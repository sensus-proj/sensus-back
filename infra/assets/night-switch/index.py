"""Pausa nocturna de la infraestructura de Sensus (Spec 051).

Acciones:
- ``stop``: corta el tráfico de la API (concurrencia reservada en 0), silencia las alarmas, elimina la ruta de
  salida a internet y borra el NAT Gateway y su IP elástica, que son los recursos que cobran por hora.
- ``start``: crea la IP elástica y el NAT Gateway, restaura la ruta de salida, reactiva las alarmas y devuelve la
  concurrencia a la API. La concurrencia se restaura al final para no aceptar tráfico sin salida a internet.
- ``reconcile``: aplica ``stop`` o ``start`` según la hora local (se usa al desplegar).

Seguridad: solo toca recursos con la etiqueta ``sensus:night-switch=<entorno>`` y las tablas de rutas, la
función y las alarmas que recibe por variables de entorno. No registra secretos ni datos personales.
"""

import datetime
import json
import logging
import os
import time
from zoneinfo import ZoneInfo

import boto3
from botocore.exceptions import ClientError

LOG = logging.getLogger()
LOG.setLevel(logging.INFO)

EC2 = boto3.client("ec2")
LAMBDA = boto3.client("lambda")
CLOUDWATCH = boto3.client("cloudwatch")

TAG_KEY = "sensus:night-switch"
ENV_NAME = os.environ["ENV_NAME"]
API_FUNCTION = os.environ["API_FUNCTION_NAME"]
PUBLIC_SUBNET_ID = os.environ["PUBLIC_SUBNET_ID"]
PRIVATE_ROUTE_TABLE_IDS = [r for r in os.environ["PRIVATE_ROUTE_TABLE_IDS"].split(",") if r]
ALARM_NAMES = [a for a in os.environ.get("ALARM_NAMES", "").split(",") if a]
TIME_ZONE = os.environ.get("SCHEDULE_TIME_ZONE", "America/Mexico_City")
STOP_AT = os.environ.get("STOP_AT", "22:30")
START_AT = os.environ.get("START_AT", "07:00")
DEFAULT_ROUTE = "0.0.0.0/0"
WAIT_SECONDS = 600


def handler(event, _context):
    action = (event or {}).get("action", "reconcile")
    if action == "reconcile":
        action = "stop" if is_night(datetime.datetime.now(ZoneInfo(TIME_ZONE)).time()) else "start"

    if action == "stop":
        stop()
    elif action == "start":
        start()
    else:
        raise ValueError(f"Acción no soportada: {action}")

    LOG.info(json.dumps({"event": "night-switch", "action": action, "environment": ENV_NAME}))
    return {"action": action}


def parse(value):
    hours, minutes = value.split(":")
    return datetime.time(int(hours), int(minutes))


def is_night(now):
    stop_at, start_at = parse(STOP_AT), parse(START_AT)
    if stop_at > start_at:
        return now >= stop_at or now < start_at
    return stop_at <= now < start_at


def tags(resource_type):
    return [{
        "ResourceType": resource_type,
        "Tags": [
            {"Key": TAG_KEY, "Value": ENV_NAME},
            {"Key": "project", "Value": "sensus"},
            {"Key": "environment", "Value": ENV_NAME},
            {"Key": "owner", "Value": "sensus-team"},
        ],
    }]


def tag_filter():
    return {"Name": f"tag:{TAG_KEY}", "Values": [ENV_NAME]}


def live_nat_gateways():
    response = EC2.describe_nat_gateways(Filters=[tag_filter(), {"Name": "state", "Values": ["pending", "available"]}])
    return response.get("NatGateways", [])


def start():
    gateways = live_nat_gateways()
    if gateways:
        nat_id = gateways[0]["NatGatewayId"]
    else:
        allocation = EC2.allocate_address(Domain="vpc", TagSpecifications=tags("elastic-ip"))
        nat = EC2.create_nat_gateway(
            SubnetId=PUBLIC_SUBNET_ID,
            AllocationId=allocation["AllocationId"],
            ConnectivityType="public",
            TagSpecifications=tags("natgateway"),
        )
        nat_id = nat["NatGateway"]["NatGatewayId"]

    EC2.get_waiter("nat_gateway_available").wait(
        NatGatewayIds=[nat_id], WaiterConfig={"Delay": 10, "MaxAttempts": WAIT_SECONDS // 10})

    for route_table_id in PRIVATE_ROUTE_TABLE_IDS:
        try:
            EC2.replace_route(RouteTableId=route_table_id, DestinationCidrBlock=DEFAULT_ROUTE, NatGatewayId=nat_id)
        except ClientError as error:
            if error.response["Error"]["Code"] != "InvalidParameterValue" and "NotFound" not in error.response["Error"]["Code"]:
                raise
            EC2.create_route(RouteTableId=route_table_id, DestinationCidrBlock=DEFAULT_ROUTE, NatGatewayId=nat_id)

    if ALARM_NAMES:
        CLOUDWATCH.enable_alarm_actions(AlarmNames=ALARM_NAMES)

    try:
        LAMBDA.delete_function_concurrency(FunctionName=API_FUNCTION)
    except ClientError as error:
        if error.response["Error"]["Code"] != "ResourceNotFoundException":
            raise


def stop():
    # Primero se corta el tráfico y se silencian las alarmas, para no notificar fallos esperados.
    LAMBDA.put_function_concurrency(FunctionName=API_FUNCTION, ReservedConcurrentExecutions=0)
    if ALARM_NAMES:
        CLOUDWATCH.disable_alarm_actions(AlarmNames=ALARM_NAMES)

    for route_table_id in PRIVATE_ROUTE_TABLE_IDS:
        try:
            EC2.delete_route(RouteTableId=route_table_id, DestinationCidrBlock=DEFAULT_ROUTE)
        except ClientError as error:
            if "NotFound" not in error.response["Error"]["Code"]:
                raise

    gateway_ids = [g["NatGatewayId"] for g in live_nat_gateways()]
    for gateway_id in gateway_ids:
        EC2.delete_nat_gateway(NatGatewayId=gateway_id)
    wait_deleted(gateway_ids)

    addresses = EC2.describe_addresses(Filters=[tag_filter()]).get("Addresses", [])
    for address in addresses:
        if not address.get("AssociationId"):
            EC2.release_address(AllocationId=address["AllocationId"])


def wait_deleted(gateway_ids):
    deadline = time.time() + WAIT_SECONDS
    pending = set(gateway_ids)
    while pending and time.time() < deadline:
        response = EC2.describe_nat_gateways(NatGatewayIds=list(pending))
        pending = {g["NatGatewayId"] for g in response.get("NatGateways", []) if g["State"] != "deleted"}
        if pending:
            time.sleep(10)
    if pending:
        raise TimeoutError(f"Los NAT Gateway no terminaron de borrarse: {sorted(pending)}")
