using Amazon.CDK;
using Amazon.CDK.AWS.CloudWatch;
using Amazon.CDK.AWS.EC2;
using Amazon.CDK.AWS.ElastiCache;
using Amazon.CDK.AWS.IAM;
using Amazon.CDK.AWS.Lambda;
using Amazon.CDK.AWS.Logs;
using Amazon.CDK.AWS.Scheduler;
using Amazon.CDK.Triggers;

namespace Sensus.Infra;

/// <summary>
/// Red privada, caché Valkey (compatible con Redis) y pausa nocturna (Spec 048 FR-048-14 y Spec 051, ADR 0014).
/// </summary>
public sealed partial class SensusInfraStack
{
    private const string NightSwitchTagKey = "sensus:night-switch";
    private static readonly string[] NatCreateActions = ["AllocateAddress", "CreateNatGateway"];

    /// <summary>Recursos de red y caché que la función de la API necesita.</summary>
    private sealed record CacheNetwork(Vpc Vpc, SecurityGroup ApiSecurityGroup, CfnServerlessCache Cache, string RedisConnectionString);

    /// <summary>
    /// VPC de dos zonas sin NAT fijo: el NAT lo crea y destruye la pausa nocturna para no pagarlo de noche.
    /// La caché solo acepta conexiones TLS en el puerto 6379 desde el grupo de seguridad de la API.
    /// </summary>
    private CacheNetwork CreateCacheNetwork(string envName)
    {
        var vpc = new Vpc(this, "SensusVpc", new VpcProps
        {
            VpcName = $"sensus-vpc-{envName}",
            IpAddresses = IpAddresses.Cidr("10.40.0.0/16"),
            MaxAzs = 2,
            NatGateways = 0,
            RestrictDefaultSecurityGroup = true,
            SubnetConfiguration =
            [
                new SubnetConfiguration { Name = "public", SubnetType = SubnetType.PUBLIC, CidrMask = 24 },
                new SubnetConfiguration { Name = "private", SubnetType = SubnetType.PRIVATE_ISOLATED, CidrMask = 24 }
            ]
        });

        // DynamoDB por endpoint de puerta de enlace (gratuito): los contadores de Sensi no dependen del NAT.
        vpc.AddGatewayEndpoint("DynamoDbGatewayEndpoint", new GatewayVpcEndpointOptions
        {
            Service = GatewayVpcEndpointAwsService.DYNAMODB,
            Subnets = [new SubnetSelection { SubnetType = SubnetType.PRIVATE_ISOLATED }]
        });

        var apiSecurityGroup = new SecurityGroup(this, "SensusApiSecurityGroup", new SecurityGroupProps
        {
            Vpc = vpc,
            SecurityGroupName = $"sensus-api-{envName}",
            Description = "Lambda de Sensus: salida a internet por NAT y a la cache Valkey",
            AllowAllOutbound = true
        });

        var cacheSecurityGroup = new SecurityGroup(this, "SensusCacheSecurityGroup", new SecurityGroupProps
        {
            Vpc = vpc,
            SecurityGroupName = $"sensus-cache-{envName}",
            Description = "Cache Valkey de Sensus: solo TLS 6379 desde la Lambda de la API",
            AllowAllOutbound = false
        });
        cacheSecurityGroup.AddIngressRule(apiSecurityGroup, Port.Tcp(6379), "TLS desde la Lambda de la API");

        // Valkey Serverless: el mínimo facturable es 100 MB (Redis OSS exige 1 GB). Los límites acotan el costo.
        var cache = new CfnServerlessCache(this, "SensusValkeyCache", new CfnServerlessCacheProps
        {
            ServerlessCacheName = $"sensus-cache-{envName}",
            Description = "Cache de lecturas recurrentes de Sensus (Spec 048)",
            Engine = "valkey",
            MajorEngineVersion = "8",
            SecurityGroupIds = [cacheSecurityGroup.SecurityGroupId],
            SubnetIds = vpc.IsolatedSubnets.Select(s => s.SubnetId).ToArray(),
            CacheUsageLimits = new CfnServerlessCache.CacheUsageLimitsProperty
            {
                DataStorage = new CfnServerlessCache.DataStorageProperty { Maximum = 1, Unit = "GB" },
                EcpuPerSecond = new CfnServerlessCache.ECPUPerSecondProperty { Maximum = 5000 }
            }
        });

        string connectionString = Fn.Join(string.Empty,
        [
            cache.AttrEndpointAddress, ":", cache.AttrEndpointPort, ",ssl=True,abortConnect=False,connectTimeout=2000"
        ]);

        return new CacheNetwork(vpc, apiSecurityGroup, cache, connectionString);
    }

    /// <summary>
    /// Pausa nocturna (Spec 051): de <paramref name="stopAt"/> a <paramref name="startAt"/> la API deja de aceptar
    /// tráfico y se elimina el NAT Gateway, el único recurso con costo por hora que se puede apagar.
    /// </summary>
    private void CreateNightlyPause(
        string envName,
        CacheNetwork network,
        Function apiFunction,
        IEnumerable<Alarm> alarms,
        string timeZone,
        string stopAt,
        string startAt)
    {
        var logGroup = new LogGroup(this, "SensusNightSwitchLogGroup", new LogGroupProps
        {
            LogGroupName = $"/aws/lambda/sensus-night-switch-{envName}",
            Retention = RetentionDays.ONE_WEEK,
            RemovalPolicy = RemovalPolicy.DESTROY
        });

        var role = new Role(this, "SensusNightSwitchRole", new RoleProps
        {
            RoleName = $"sensus-night-switch-role-{envName}",
            AssumedBy = new ServicePrincipal("lambda.amazonaws.com"),
            Description = "Pausa nocturna de Sensus: NAT, rutas privadas, concurrencia de la API y acciones de alarmas"
        });

        role.AddToPolicy(new PolicyStatement(new PolicyStatementProps
        {
            Sid = "Logs",
            Actions = ["logs:CreateLogStream", "logs:PutLogEvents"],
            Resources = [logGroup.LogGroupArn, $"{logGroup.LogGroupArn}:*"]
        }));

        // Las acciones Describe no admiten restricción por recurso en EC2.
        role.AddToPolicy(new PolicyStatement(new PolicyStatementProps
        {
            Sid = "DescribeNetwork",
            Actions = ["ec2:DescribeNatGateways", "ec2:DescribeAddresses", "ec2:DescribeRouteTables"],
            Resources = ["*"]
        }));

        // CreateNatGateway se autoriza por separado sobre cada recurso de la solicitud (NAT, subred e IP elástica), y
        // aws:RequestTag solo existe para el recurso que se crea. Por eso cada recurso lleva su propia condición
        // (la v1 usaba una sola condición de RequestTag y AWS negaba el uso de la IP elástica).
        role.AddToPolicy(new PolicyStatement(new PolicyStatementProps
        {
            Sid = "AllocateOnlyTaggedAddress",
            Actions = ["ec2:AllocateAddress"],
            Resources = ["*"],
            Conditions = new Dictionary<string, object>
            {
                ["StringEquals"] = new Dictionary<string, string> { [$"aws:RequestTag/{NightSwitchTagKey}"] = envName }
            }
        }));

        role.AddToPolicy(new PolicyStatement(new PolicyStatementProps
        {
            Sid = "CreateOnlyTaggedNatGateway",
            Actions = ["ec2:CreateNatGateway"],
            Resources = [$"arn:aws:ec2:{Region}:{Account}:natgateway/*"],
            Conditions = new Dictionary<string, object>
            {
                ["StringEquals"] = new Dictionary<string, string> { [$"aws:RequestTag/{NightSwitchTagKey}"] = envName }
            }
        }));

        role.AddToPolicy(new PolicyStatement(new PolicyStatementProps
        {
            Sid = "NatUsesOnlyTaggedAddress",
            Actions = ["ec2:CreateNatGateway"],
            Resources = [$"arn:aws:ec2:{Region}:{Account}:elastic-ip/*"],
            Conditions = new Dictionary<string, object>
            {
                ["StringEquals"] = new Dictionary<string, string> { [$"ec2:ResourceTag/{NightSwitchTagKey}"] = envName }
            }
        }));

        role.AddToPolicy(new PolicyStatement(new PolicyStatementProps
        {
            Sid = "NatUsesOnlyThePublicSubnet",
            Actions = ["ec2:CreateNatGateway"],
            Resources = [$"arn:aws:ec2:{Region}:{Account}:subnet/{network.Vpc.PublicSubnets[0].SubnetId}"]
        }));

        role.AddToPolicy(new PolicyStatement(new PolicyStatementProps
        {
            Sid = "TagOnCreateOnly",
            Actions = ["ec2:CreateTags"],
            Resources =
            [
                $"arn:aws:ec2:{Region}:{Account}:elastic-ip/*",
                $"arn:aws:ec2:{Region}:{Account}:natgateway/*"
            ],
            Conditions = new Dictionary<string, object>
            {
                ["StringEquals"] = new Dictionary<string, object> { ["ec2:CreateAction"] = NatCreateActions }
            }
        }));

        // Borrado solo de recursos etiquetados por la propia pausa nocturna.
        role.AddToPolicy(new PolicyStatement(new PolicyStatementProps
        {
            Sid = "DeleteOnlyTaggedNatAndAddress",
            Actions = ["ec2:DeleteNatGateway", "ec2:ReleaseAddress"],
            Resources =
            [
                $"arn:aws:ec2:{Region}:{Account}:elastic-ip/*",
                $"arn:aws:ec2:{Region}:{Account}:natgateway/*"
            ],
            Conditions = new Dictionary<string, object>
            {
                ["StringEquals"] = new Dictionary<string, string> { [$"ec2:ResourceTag/{NightSwitchTagKey}"] = envName }
            }
        }));

        string[] privateRouteTables = network.Vpc.IsolatedSubnets.Select(s => s.RouteTable.RouteTableId).ToArray();
        role.AddToPolicy(new PolicyStatement(new PolicyStatementProps
        {
            Sid = "ManageDefaultRouteOfPrivateSubnets",
            Actions = ["ec2:CreateRoute", "ec2:ReplaceRoute", "ec2:DeleteRoute"],
            Resources = privateRouteTables.Select(id => $"arn:aws:ec2:{Region}:{Account}:route-table/{id}").ToArray()
        }));

        role.AddToPolicy(new PolicyStatement(new PolicyStatementProps
        {
            Sid = "ToggleApiConcurrency",
            Actions = ["lambda:PutFunctionConcurrency", "lambda:DeleteFunctionConcurrency"],
            Resources = [apiFunction.FunctionArn]
        }));

        Alarm[] alarmList = alarms.ToArray();
        role.AddToPolicy(new PolicyStatement(new PolicyStatementProps
        {
            Sid = "ToggleAlarmActions",
            Actions = ["cloudwatch:EnableAlarmActions", "cloudwatch:DisableAlarmActions"],
            Resources = alarmList.Select(a => a.AlarmArn).ToArray()
        }));

        var nightSwitch = new Function(this, "SensusNightSwitchFunction", new FunctionProps
        {
            FunctionName = $"sensus-night-switch-{envName}",
            Description = $"Pausa la infraestructura de Sensus de {stopAt} a {startAt} ({timeZone})",
            Runtime = Runtime.PYTHON_3_12,
            Architecture = Architecture.ARM_64,
            Handler = "index.handler",
            Code = Code.FromAsset(ResolveAssetDirectory("night-switch")),
            Role = role,
            MemorySize = 128,
            Timeout = Duration.Minutes(14),
            LogGroup = logGroup,
            ReservedConcurrentExecutions = 1,
            Environment = new Dictionary<string, string>
            {
                ["ENV_NAME"] = envName,
                ["API_FUNCTION_NAME"] = apiFunction.FunctionName,
                ["PUBLIC_SUBNET_ID"] = network.Vpc.PublicSubnets[0].SubnetId,
                ["PRIVATE_ROUTE_TABLE_IDS"] = Fn.Join(",", privateRouteTables),
                ["ALARM_NAMES"] = Fn.Join(",", alarmList.Select(a => a.AlarmName).ToArray()),
                ["SCHEDULE_TIME_ZONE"] = timeZone,
                ["STOP_AT"] = stopAt,
                ["START_AT"] = startAt
            }
        });

        var schedulerRole = new Role(this, "SensusNightSwitchSchedulerRole", new RoleProps
        {
            RoleName = $"sensus-night-switch-scheduler-{envName}",
            AssumedBy = new ServicePrincipal("scheduler.amazonaws.com", new ServicePrincipalOpts
            {
                // Protección contra el "confused deputy": solo esquemas de esta cuenta.
                Conditions = new Dictionary<string, object>
                {
                    ["StringEquals"] = new Dictionary<string, string> { ["aws:SourceAccount"] = Account }
                }
            })
        });
        nightSwitch.GrantInvoke(schedulerRole);

        AddSchedule("SensusNightStopSchedule", $"sensus-night-stop-{envName}", stopAt, "stop", timeZone, nightSwitch, schedulerRole);
        AddSchedule("SensusMorningStartSchedule", $"sensus-morning-start-{envName}", startAt, "start", timeZone, nightSwitch, schedulerRole);

        // Al desplegar se aplica el estado que corresponde a la hora actual (crea el NAT si es de día).
        _ = new Trigger(this, "SensusNightSwitchReconcile", new TriggerProps
        {
            Handler = nightSwitch,
            ExecuteAfter = [apiFunction, network.Cache],
            ExecuteOnHandlerChange = true,
            InvocationType = InvocationType.REQUEST_RESPONSE,
            Timeout = Duration.Minutes(14)
        });

        _ = new CfnOutput(this, "NightSwitchFunctionName", new CfnOutputProps
        {
            Description = "Función que pausa y reanuda la infraestructura (invocable a mano con {\"action\":\"start\"})",
            Value = nightSwitch.FunctionName
        });
    }

    private void AddSchedule(string id, string name, string at, string action, string timeZone, Function target, Role role)
    {
        string[] parts = at.Split(':');
        _ = new CfnSchedule(this, id, new CfnScheduleProps
        {
            Name = name,
            Description = $"Sensus: {action} a las {at} ({timeZone})",
            ScheduleExpression = $"cron({int.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture)} {int.Parse(parts[0], System.Globalization.CultureInfo.InvariantCulture)} * * ? *)",
            ScheduleExpressionTimezone = timeZone,
            FlexibleTimeWindow = new CfnSchedule.FlexibleTimeWindowProperty { Mode = "OFF" },
            Target = new CfnSchedule.TargetProperty
            {
                Arn = target.FunctionArn,
                RoleArn = role.RoleArn,
                Input = $"{{\"action\":\"{action}\"}}",
                RetryPolicy = new CfnSchedule.RetryPolicyProperty { MaximumRetryAttempts = 2, MaximumEventAgeInSeconds = 3600 }
            }
        });
    }

    private static string ResolveAssetDirectory(string name)
    {
        var cwd = Directory.GetCurrentDirectory();
        foreach (string candidate in new[] { Path.Combine(cwd, "assets", name), Path.Combine(cwd, "infra", "assets", name) })
        {
            if (Directory.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new DirectoryNotFoundException($"No se encontró infra/assets/{name}.");
    }
}
