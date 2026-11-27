import datetime
import importlib
import logging
import os
import sys
import types
import unittest
from unittest.mock import MagicMock, patch

MODULE_DIR = os.path.dirname(os.path.abspath(__file__))
if MODULE_DIR not in sys.path:
    sys.path.insert(0, MODULE_DIR)

ENV_VARS = {
    "ENV_NAME": "prod",
    "API_FUNCTION_NAME": "sensus-api-prod",
    "PUBLIC_SUBNET_ID": "subnet-0123456789abcdef0",
    "PRIVATE_ROUTE_TABLE_IDS": "rtb-11111111,rtb-22222222",
    "ALARM_NAMES": "alarm-5xx,alarm-lambda",
    "SCHEDULE_TIME_ZONE": "America/Mexico_City",
    "STOP_AT": "22:30",
    "START_AT": "07:00",
}


class FakeClientError(Exception):
    def __init__(self, error_response, operation_name="Operation"):
        super().__init__(operation_name)
        self.response = error_response


def load_index_module():
    ec2 = MagicMock()
    lam = MagicMock()
    cw = MagicMock()
    clients = {"ec2": ec2, "lambda": lam, "cloudwatch": cw}

    fake_boto3 = types.ModuleType("boto3")
    fake_boto3.client = MagicMock(side_effect=lambda name, *args, **kwargs: clients[name])

    fake_botocore = types.ModuleType("botocore")
    fake_botocore_exc = types.ModuleType("botocore.exceptions")
    fake_botocore_exc.ClientError = FakeClientError
    fake_botocore.exceptions = fake_botocore_exc

    with patch.dict(os.environ, ENV_VARS, clear=False), patch.dict(
        sys.modules,
        {
            "boto3": fake_boto3,
            "botocore": fake_botocore,
            "botocore.exceptions": fake_botocore_exc,
        },
    ):
        sys.modules.pop("index", None)
        module = importlib.import_module("index")
    return module, ec2, lam, cw


class NightSwitchTests(unittest.TestCase):
    def test_is_night_boundaries(self):
        mod, _, _, _ = load_index_module()
        self.assertFalse(mod.is_night(datetime.time(22, 29)))
        self.assertTrue(mod.is_night(datetime.time(22, 30)))
        self.assertTrue(mod.is_night(datetime.time(6, 59)))
        self.assertFalse(mod.is_night(datetime.time(7, 0)))

    def test_reconcile_chooses_action_by_local_time(self):
        mod, _, _, _ = load_index_module()
        with patch.object(mod, "stop") as stop_mock, patch.object(mod, "start") as start_mock:
            with patch.object(mod, "is_night", return_value=True):
                res_stop = mod.handler({"action": "reconcile"}, None)
                self.assertEqual(res_stop, {"action": "stop"})
                stop_mock.assert_called_once()
                start_mock.assert_not_called()

            stop_mock.reset_mock()
            start_mock.reset_mock()

            with patch.object(mod, "is_night", return_value=False):
                res_start = mod.handler({"action": "reconcile"}, None)
                self.assertEqual(res_start, {"action": "start"})
                start_mock.assert_called_once()
                stop_mock.assert_not_called()

    def test_unknown_action_raises_value_error(self):
        mod, _, _, _ = load_index_module()
        with self.assertRaises(ValueError):
            mod.handler({"action": "pause-forever"}, None)

    def test_stop_zeros_concurrency_and_only_targets_tagged_resources(self):
        mod, ec2, lam, cw = load_index_module()
        ec2.describe_nat_gateways.side_effect = [
            {"NatGateways": [{"NatGatewayId": "nat-tagged"}]},
            {"NatGateways": [{"NatGatewayId": "nat-tagged", "State": "deleted"}]},
        ]
        ec2.describe_addresses.return_value = {
            "Addresses": [
                {"AllocationId": "eipalloc-free", "AssociationId": None},
                {"AllocationId": "eipalloc-busy", "AssociationId": "eipassoc-1"},
            ]
        }

        mod.handler({"action": "stop"}, None)

        lam.put_function_concurrency.assert_called_once_with(
            FunctionName="sensus-api-prod", ReservedConcurrentExecutions=0
        )
        cw.disable_alarm_actions.assert_called_once_with(AlarmNames=["alarm-5xx", "alarm-lambda"])
        expected_tag_filter = {"Name": "tag:sensus:night-switch", "Values": ["prod"]}
        self.assertEqual(
            ec2.describe_nat_gateways.call_args_list[0].kwargs["Filters"],
            [expected_tag_filter, {"Name": "state", "Values": ["pending", "available"]}],
        )
        ec2.delete_nat_gateway.assert_called_once_with(NatGatewayId="nat-tagged")
        ec2.describe_addresses.assert_called_once_with(Filters=[expected_tag_filter])
        ec2.release_address.assert_called_once_with(AllocationId="eipalloc-free")

    def test_start_restores_lambda_concurrency_last(self):
        mod, ec2, lam, cw = load_index_module()
        call_order = []

        ec2.describe_nat_gateways.return_value = {"NatGateways": []}
        ec2.describe_addresses.side_effect = [{"Addresses": []}, {"Addresses": []}]
        ec2.allocate_address.side_effect = lambda **kw: call_order.append("allocate") or {"AllocationId": "eip-1"}
        ec2.create_nat_gateway.side_effect = lambda **kw: call_order.append("create_nat") or {
            "NatGateway": {"NatGatewayId": "nat-1"}
        }
        waiter = MagicMock()
        waiter.wait.side_effect = lambda **kw: call_order.append("wait_nat")
        ec2.get_waiter.return_value = waiter
        ec2.replace_route.side_effect = lambda **kw: call_order.append(f"route:{kw['RouteTableId']}")
        cw.enable_alarm_actions.side_effect = lambda **kw: call_order.append("enable_alarms")
        lam.delete_function_concurrency.side_effect = lambda **kw: call_order.append("restore_concurrency")

        mod.handler({"action": "start"}, None)

        self.assertEqual(
            call_order,
            [
                "allocate",
                "create_nat",
                "wait_nat",
                "route:rtb-11111111",
                "route:rtb-22222222",
                "enable_alarms",
                "restore_concurrency",
            ],
        )
        self.assertEqual(call_order[-1], "restore_concurrency")

    def test_logs_never_expose_env_ids_or_secrets(self):
        mod, ec2, lam, cw = load_index_module()
        ec2.describe_nat_gateways.return_value = {"NatGateways": [{"NatGatewayId": "nat-1"}]}
        ec2.get_waiter.return_value = MagicMock()
        ec2.describe_addresses.return_value = {"Addresses": []}

        with self.assertLogs(mod.LOG, level=logging.INFO) as captured:
            mod.handler({"action": "start"}, None)

        joined = "\n".join(captured.output)
        for sensitive in ("subnet-0123456789abcdef0", "rtb-11111111", "rtb-22222222", "alarm-5xx", "sensus-api-prod"):
            self.assertNotIn(sensitive, joined)


if __name__ == "__main__":
    unittest.main()
