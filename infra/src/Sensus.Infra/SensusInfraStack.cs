using Amazon.CDK;
using Amazon.CDK.AWS.Apigatewayv2;
using Amazon.CDK.AWS.Budgets;
using Amazon.CDK.AWS.CloudWatch;
using Amazon.CDK.AWS.CloudWatch.Actions;
using Amazon.CDK.AWS.DynamoDB;
using Amazon.CDK.AWS.EC2;
using Amazon.CDK.AWS.IAM;
using Amazon.CDK.AWS.Lambda;
using Amazon.CDK.AWS.Logs;
using Amazon.CDK.AWS.SNS;
using Constructs;
using CfnRoute = Amazon.CDK.AWS.Apigatewayv2.CfnRoute;
using CfnRouteProps = Amazon.CDK.AWS.Apigatewayv2.CfnRouteProps;

namespace Sensus.Infra;

/// <summary>
/// Stack de Infraestructura Serverless de Costo Cero para Sensus Backend (Spec 045 — Principio VI).
/// Aprovisiona AWS Lambda (.NET 10 en Amazon Linux 2023 ARM64), AWS API Gateway v2 (HTTP API),
/// Amazon CloudWatch Logs (7 días de retención), alarmas operativas, acceso a SSM Parameter Store
/// y alarma presupuestaria en AWS Budgets.
/// </summary>
public sealed partial class SensusInfraStack : Stack
{
    /// <summary>Espacio de nombres de las métricas de aplicación; coincide con <c>Observability:Metrics:Namespace</c>.</summary>
    public const string AppMetricsNamespace = "Sensus/Api";

    public SensusInfraStack(Construct scope, string id, IStackProps? props = null)
        : base(scope, id, props)
    {
        var envName = (Node.TryGetContext("environment") as string ?? "prod").ToLowerInvariant();
        // Correo de alarmas: llega por contexto desde el SECRETO de GitHub ALERTS_EMAIL; nunca se versiona (FR-045-12).
        // Sin correo no se usa un relleno: el workflow de despliegue falla antes (FR-045-15) y la síntesis de CI no crea suscripción.
        string? alertsEmail = Node.TryGetContext("budgetEmail") is string email && !string.IsNullOrWhiteSpace(email)
            ? email.Trim()
            : null;
        var allowedOrigins = ResolveAllowedOrigins();

        // Spec 051: pausa nocturna configurable por contexto (por defecto 22:30 a 07:00, hora del centro de México).
        var scheduleTimeZone = Node.TryGetContext("scheduleTimeZone") as string ?? "America/Mexico_City";
        var nightStopAt = Node.TryGetContext("nightStopAt") as string ?? "22:30";
        var morningStartAt = Node.TryGetContext("morningStartAt") as string ?? "07:00";
        var monthlyBudgetUsd = double.TryParse(
            Node.TryGetContext("monthlyBudgetUsd")?.ToString(),
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture,
            out var parsedBudget) ? parsedBudget : 40.0;

        // 1. Grupo de Logs en Amazon CloudWatch con retención de 7 días (FR-045-05, NFR-045-03)
        // Garantiza mantenerse permanentemente dentro de los 5 GB gratuitos de CloudWatch.
        var apiLogGroup = new LogGroup(this, "SensusApiLogGroup", new LogGroupProps
        {
            LogGroupName = $"/aws/lambda/sensus-api-{envName}",
            Retention = RetentionDays.ONE_WEEK,
            RemovalPolicy = RemovalPolicy.DESTROY
        });

        // 2. Rol IAM de Mínimo Privilegio para la función Lambda (FR-045-08, NFR-045-04)
        var lambdaRole = new Role(this, "SensusApiLambdaRole", new RoleProps
        {
            RoleName = $"sensus-api-lambda-role-{envName}",
            AssumedBy = new ServicePrincipal("lambda.amazonaws.com"),
            Description = "Rol IAM de minimo privilegio para Sensus.Api Lambda (CloudWatch Logs + SSM Parameter Store + SES)"
        });

        lambdaRole.AddToPolicy(new PolicyStatement(new PolicyStatementProps
        {
            Sid = "AllowCloudWatchLogs",
            Effect = Effect.ALLOW,
            Actions = ["logs:CreateLogStream", "logs:PutLogEvents"],
            Resources = [apiLogGroup.LogGroupArn, $"{apiLogGroup.LogGroupArn}:*"]
        }));

        // Contadores atómicos de presupuesto y cuota de Sensi IA (Spec 049, ADR 0013).
        // PAY_PER_REQUEST + TTL nativo: dentro del Free Tier permanente de DynamoDB (0 USD) y sin VPC.
        var budgetTable = new Table(this, "SensiBudgetTable", new TableProps
        {
            TableName = $"sensus-sensi-budget-{envName}",
            PartitionKey = new Amazon.CDK.AWS.DynamoDB.Attribute { Name = "pk", Type = AttributeType.STRING },
            BillingMode = BillingMode.PAY_PER_REQUEST,
            TimeToLiveAttribute = "expiresAt",
            PointInTimeRecoverySpecification = new PointInTimeRecoverySpecification { PointInTimeRecoveryEnabled = false },
            RemovalPolicy = RemovalPolicy.DESTROY
        });

        lambdaRole.AddToPolicy(new PolicyStatement(new PolicyStatementProps
        {
            Sid = "AllowSensiBudgetCounters",
            Effect = Effect.ALLOW,
            Actions = ["dynamodb:GetItem", "dynamodb:UpdateItem"],
            Resources = [budgetTable.TableArn]
        }));

        // Correo transaccional de recuperación de contraseña por Amazon SES (Spec 029, FR-029-13). Solo envío
        // (SendEmail de la API v1 que usa AWSSDK.SimpleEmail), restringido a identidades verificadas de esta cuenta y
        // región. El remitente vive en SSM (/sensus/<env>/SesEmailSettings/From) y no se versiona. Free Tier: 3,000/mes.
        lambdaRole.AddToPolicy(new PolicyStatement(new PolicyStatementProps
        {
            Sid = "AllowSesSendTransactionalEmail",
            Effect = Effect.ALLOW,
            Actions = ["ses:SendEmail"],
            Resources =
            [
                $"arn:aws:ses:{Region}:{Account}:identity/*",
                $"arn:aws:ses:{Region}:{Account}:configuration-set/sensus-transactional-{envName}"
            ]
        }));

        // Lectura de parámetros y secretos desde AWS Systems Manager (SSM) Parameter Store (100 % gratuito, 0 USD)
        lambdaRole.AddToPolicy(new PolicyStatement(new PolicyStatementProps
        {
            Sid = "AllowSsmParameterStoreRead",
            Effect = Effect.ALLOW,
            Actions = ["ssm:GetParameter", "ssm:GetParameters", "ssm:GetParametersByPath"],
            // GetParametersByPath se autoriza sobre la ruta misma; GetParameter(s), sobre cada parámetro.
            Resources =
            [
                $"arn:aws:ssm:{Region}:{Account}:parameter/sensus/{envName}",
                $"arn:aws:ssm:{Region}:{Account}:parameter/sensus/{envName}/*"
            ]
        }));

        // 3. Función AWS Lambda para .NET 10 sobre Amazon Linux 2023 ARM64 / Graviton2 (FR-045-03)
        // Spec 048 (FR-048-14) y ADR 0014: la Lambda vive en subredes privadas para alcanzar la caché Valkey; sale a
        // MongoDB Atlas, Cloudinary, OpenAI y FCM por un NAT que la pausa nocturna crea de día y destruye de noche.
        var assetPath = ResolveLambdaAssetPath();
        var cacheNetwork = CreateCacheNetwork(envName);
        lambdaRole.AddManagedPolicy(ManagedPolicy.FromAwsManagedPolicyName("service-role/AWSLambdaVPCAccessExecutionRole"));

        var lambdaEnv = new Dictionary<string, string>
        {
            ["ASPNETCORE_ENVIRONMENT"] = "Production",
            ["AWS_SSM_PARAMETER_PREFIX"] = $"/sensus/{envName}",
            ["Redis__Enabled"] = "true",
            ["Redis__ConnectionString"] = cacheNetwork.RedisConnectionString,
            ["AiBudgetStore__TableName"] = budgetTable.TableName,
            ["Observability__Metrics__Enabled"] = "true",
            ["Observability__Metrics__Namespace"] = AppMetricsNamespace,
            ["Interactions__Enabled"] = "true",
            ["DOTNET_SYSTEM_GLOBALIZATION_INVARIANT"] = "true"
        };

        // Los secretos (MongoDB, JWT, código de apartado, Cloudinary, OpenAI, correo, clave HMAC de interacciones
        // /sensus/<env>/Interactions/HmacKeysJson) ya no viajan como variables de entorno: la API los lee de SSM
        // Parameter Store bajo AWS_SSM_PARAMETER_PREFIX (Principio VIII, brecha G-01 y Spec 055 FR-055-04).

        var apiFunction = new Function(this, "SensusApiFunction", new FunctionProps
        {
            FunctionName = $"sensus-api-{envName}",
            Description = "Sensus Backend RESTful API (.NET 10 LTS Serverless sobre Amazon Linux 2023 ARM64)",
            Runtime = Runtime.PROVIDED_AL2023,
            Architecture = Architecture.ARM_64,
            Handler = "bootstrap",
            Code = Code.FromAsset(assetPath),
            Role = lambdaRole,
            Vpc = cacheNetwork.Vpc,
            VpcSubnets = new SubnetSelection { SubnetType = SubnetType.PRIVATE_ISOLATED },
            SecurityGroups = [cacheNetwork.ApiSecurityGroup],
            MemorySize = 512,
            Timeout = Duration.Seconds(29),
            LogGroup = apiLogGroup,
            Environment = lambdaEnv
        });

        // 4. AWS API Gateway v2 — HTTP API con integración proxy hacia la Lambda y CORS (FR-045-04)
        var httpApi = new CfnApi(this, "SensusHttpApi", new CfnApiProps
        {
            Name = $"sensus-http-api-{envName}",
            ProtocolType = "HTTP",
            Description = "Endpoint HTTPS serverless para Sensus Backend (HTTP API v2)",
            CorsConfiguration = new CfnApi.CorsProperty
            {
                AllowOrigins = allowedOrigins,
                AllowMethods = ["GET", "POST", "PUT", "PATCH", "DELETE", "OPTIONS"],
                AllowHeaders = ["Authorization", "Content-Type", "Accept", "X-Correlation-Id"],
                ExposeHeaders = ["X-Correlation-Id"],
                MaxAge = 3600
            }
        });

        var lambdaIntegration = new Amazon.CDK.AWS.Apigatewayv2.CfnIntegration(this, "SensusLambdaIntegration", new Amazon.CDK.AWS.Apigatewayv2.CfnIntegrationProps
        {
            ApiId = httpApi.Ref,
            IntegrationType = "AWS_PROXY",
            IntegrationUri = apiFunction.FunctionArn,
            PayloadFormatVersion = "2.0",
            TimeoutInMillis = 29000
        });

        var defaultRoute = new CfnRoute(this, "SensusDefaultRoute", new CfnRouteProps
        {
            ApiId = httpApi.Ref,
            RouteKey = "$default",
            Target = $"integrations/{lambdaIntegration.Ref}"
        });

        var defaultStage = new CfnStage(this, "SensusDefaultStage", new CfnStageProps
        {
            ApiId = httpApi.Ref,
            StageName = "$default",
            AutoDeploy = true
        });
        defaultStage.Node.AddDependency(defaultRoute);

        // Permiso explícito para que API Gateway invoque la función Lambda
        apiFunction.AddPermission("AllowApiGatewayInvoke", new Permission
        {
            Principal = new ServicePrincipal("apigateway.amazonaws.com"),
            Action = "lambda:InvokeFunction",
            SourceArn = $"arn:aws:execute-api:{Region}:{Account}:{httpApi.Ref}/*"
        });

        // 5. Alarmas de Observabilidad en Amazon CloudWatch (FR-045-06, Spec 015)
        // FR-015-03: canal de notificación único (SNS con suscripción por correo) para todas las alarmas.
        var alarmEmail = Node.TryGetContext("alarmEmail") as string ?? alertsEmail;
        var alarmTopic = new Topic(this, "SensusAlarmTopic", new TopicProps
        {
            TopicName = $"sensus-alarms-{envName}",
            DisplayName = "Alarmas de Sensus Backend"
        });
        if (!string.IsNullOrWhiteSpace(alarmEmail))
        {
            // FR-045-13: identificador fijo. AddSubscription(new EmailSubscription(...)) derivaba el identificador lógico del
            // propio correo, que terminaba en la plantilla y en los logs de CI de un repositorio público.
            _ = new Subscription(this, "SensusAlarmEmailSubscription", new SubscriptionProps
            {
                Topic = alarmTopic,
                Protocol = SubscriptionProtocol.EMAIL,
                Endpoint = alarmEmail
            });
        }

        // FR-045-14: AWS Budgets publica en el tema (solo desde esta cuenta), así el presupuesto no depende del correo.
        var budgetsPublish = alarmTopic.AddToResourcePolicy(new PolicyStatement(new PolicyStatementProps
        {
            Sid = "AllowBudgetsPublish",
            Effect = Effect.ALLOW,
            Principals = [new ServicePrincipal("budgets.amazonaws.com")],
            Actions = ["SNS:Publish"],
            Resources = [alarmTopic.TopicArn],
            Conditions = new Dictionary<string, object>
            {
                ["StringEquals"] = new Dictionary<string, string> { ["aws:SourceAccount"] = Account },
                ["ArnLike"] = new Dictionary<string, string> { ["aws:SourceArn"] = $"arn:aws:budgets::{Account}:*" }
            }
        }));
        var alarmAction = new SnsAction(alarmTopic);

        var errorsAlarm = new Alarm(this, "SensusLambdaErrorsAlarm", new AlarmProps
        {
            AlarmName = $"sensus-lambda-errors-{envName}",
            AlarmDescription = "Alerta cuando la funcion Lambda de Sensus registra errores de ejecucion",
            Metric = apiFunction.MetricErrors(new MetricOptions
            {
                Period = Duration.Minutes(5),
                Statistic = "Sum"
            }),
            Threshold = 5,
            EvaluationPeriods = 1,
            ComparisonOperator = ComparisonOperator.GREATER_THAN_OR_EQUAL_TO_THRESHOLD,
            TreatMissingData = TreatMissingData.NOT_BREACHING
        });

        var throttlesAlarm = new Alarm(this, "SensusLambdaThrottlesAlarm", new AlarmProps
        {
            AlarmName = $"sensus-lambda-throttles-{envName}",
            AlarmDescription = "Alerta cuando la funcion Lambda de Sensus sufre throttling",
            Metric = apiFunction.MetricThrottles(new MetricOptions
            {
                Period = Duration.Minutes(5),
                Statistic = "Sum"
            }),
            Threshold = 1,
            EvaluationPeriods = 1,
            ComparisonOperator = ComparisonOperator.GREATER_THAN_OR_EQUAL_TO_THRESHOLD,
            TreatMissingData = TreatMissingData.NOT_BREACHING
        });

        var api5xxAlarm = new Alarm(this, "SensusApi5xxAlarm", new AlarmProps
        {
            AlarmName = $"sensus-http-api-5xx-{envName}",
            AlarmDescription = "Alerta cuando AWS API Gateway devuelve respuestas 5xx en Sensus Backend",
            Metric = new Metric(new MetricProps
            {
                Namespace = "AWS/ApiGateway",
                MetricName = "5xx",
                DimensionsMap = new Dictionary<string, string>
                {
                    ["ApiId"] = httpApi.Ref
                },
                Period = Duration.Minutes(5),
                Statistic = "Sum"
            }),
            Threshold = 5,
            EvaluationPeriods = 1,
            ComparisonOperator = ComparisonOperator.GREATER_THAN_OR_EQUAL_TO_THRESHOLD,
            TreatMissingData = TreatMissingData.NOT_BREACHING
        });

        // FR-015-01: latencia p95 de la función Lambda (el timeout configurado es de 29 s).
        var latencyAlarm = new Alarm(this, "SensusLambdaLatencyAlarm", new AlarmProps
        {
            AlarmName = $"sensus-lambda-latency-p95-{envName}",
            AlarmDescription = "Alerta cuando la latencia p95 de la funcion Lambda de Sensus supera los 3 segundos",
            Metric = apiFunction.MetricDuration(new MetricOptions
            {
                Period = Duration.Minutes(5),
                Statistic = "p95"
            }),
            Threshold = 3000,
            EvaluationPeriods = 2,
            ComparisonOperator = ComparisonOperator.GREATER_THAN_THRESHOLD,
            TreatMissingData = TreatMissingData.NOT_BREACHING
        });

        // FR-015-01: métricas de aplicación publicadas por la API con Embedded Metric Format (sin dimensiones,
        // 3 métricas personalizadas dentro de las 10 gratuitas). Se extraen del grupo de logs sin llamadas a la API.
        var appLatencyMetric = new Metric(new MetricProps
        {
            Namespace = AppMetricsNamespace,
            MetricName = "RequestLatency",
            Period = Duration.Minutes(5),
            Statistic = "p95"
        });
        var appServerErrorsMetric = new Metric(new MetricProps
        {
            Namespace = AppMetricsNamespace,
            MetricName = "ServerErrors",
            Period = Duration.Minutes(5),
            Statistic = "Sum"
        });
        var appThrottledMetric = new Metric(new MetricProps
        {
            Namespace = AppMetricsNamespace,
            MetricName = "Throttled",
            Period = Duration.Minutes(5),
            Statistic = "Sum"
        });

        // FR-015-02: rate limiting de la API (429) sostenido, señal de abuso o de umbrales mal calibrados.
        var appThrottledAlarm = new Alarm(this, "SensusAppThrottledAlarm", new AlarmProps
        {
            AlarmName = $"sensus-app-throttled-{envName}",
            AlarmDescription = "Alerta cuando el rate limiting de la API rechaza 50 o mas solicitudes en 5 minutos",
            Metric = appThrottledMetric,
            Threshold = 50,
            EvaluationPeriods = 1,
            ComparisonOperator = ComparisonOperator.GREATER_THAN_OR_EQUAL_TO_THRESHOLD,
            TreatMissingData = TreatMissingData.NOT_BREACHING
        });

        // Spec 055 (NFR-055-04, T055-08): alarma al 70 % del tope de 150 MB de offer_interactions o del clúster M0 (512 MB).
        // Usa la décima métrica personalizada gratuita (10/10) y la sexta alarma gratuita (6/10) en CloudWatch.
        var interactionsStorageMaxPctMetric = new Metric(new MetricProps
        {
            Namespace = AppMetricsNamespace,
            MetricName = "InteractionStorageMaxPct",
            Period = Duration.Hours(24),
            Statistic = "Maximum"
        });

        var interactionsStorageAlarm = new Alarm(this, "SensusInteractionsStorageAlarm", new AlarmProps
        {
            AlarmName = $"sensus-interactions-storage-70pct-{envName}",
            AlarmDescription = "Alerta cuando offer_interactions supera 70 % de 150 MB o el cluster Atlas M0 supera 70 % de 512 MB",
            Metric = interactionsStorageMaxPctMetric,
            Threshold = 70,
            EvaluationPeriods = 1,
            ComparisonOperator = ComparisonOperator.GREATER_THAN_OR_EQUAL_TO_THRESHOLD,
            TreatMissingData = TreatMissingData.NOT_BREACHING
        });

        // NFR-015-01 / Spec 055 / Spec 051: 7 alarmas en total (6 operativas + errores de la pausa), dentro de las 10 gratuitas.
        Alarm[] operationalAlarms = [errorsAlarm, throttlesAlarm, api5xxAlarm, latencyAlarm, appThrottledAlarm, interactionsStorageAlarm];
        foreach (var alarm in operationalAlarms)
        {
            alarm.AddAlarmAction(alarmAction);
        }

        // Spec 051: de noche la API no acepta tráfico y las alarmas se silencian para no notificar fallos esperados.
        // La alarma de errores de la propia pausa (FR-051-08) recibe la acción aparte para que nunca se silencie.
        CreateNightlyPause(envName, cacheNetwork, apiFunction, operationalAlarms, alarmAction, scheduleTimeZone, nightStopAt, morningStartAt);

        // Spec 029 (FR-029-14): Configuration Set de SES para rebotes, quejas y rechazos en CloudWatch.
        CreateSesMonitoring(envName, alarmAction);

        // Spec 045 (FR-045-20): roles OIDC para GitHub Actions en despliegue a producción y diff de CI.
        CreateGitHubOidcRoles(envName);

        Validations.Of(lambdaRole).Acknowledge(
            new Acknowledgment
            {
                Id = "AwsSolutions-IAM4",
                Reason = "AWSLambdaVPCAccessExecutionRole requerido por Lambda dentro de VPC para alcanzar Valkey (Spec 048 / EX-003)."
            },
            new Acknowledgment
            {
                Id = "AwsSolutions-IAM5",
                Reason = "Comodines acotados a subruta /sensus/<env>/* en SSM, streams del propio LogGroup, identidades SES de la cuenta/región e interfaces ENI de VPC."
            });

        Validations.Of(budgetTable).Acknowledge(new Acknowledgment
        {
            Id = "AwsSolutions-DDB3",
            Reason = "Contadores efímeros mensuales de presupuesto OpenAI (TTL nativo) dentro de Free Tier; PITR omitido para mantener costo 0 USD."
        });

        Validations.Of(alarmTopic).Acknowledge(
            new Acknowledgment
            {
                Id = "AwsSolutions-SNS2",
                Reason = "Tema SNS solo recibe notificaciones operativas y de presupuesto sin PII; cifrado KMS omitido para conservar costo 0 USD."
            },
            new Acknowledgment
            {
                Id = "AwsSolutions-SNS3",
                Reason = "Política restringida a budgets.amazonaws.com por SourceAccount/SourceArn y alarmas CloudWatch de la misma cuenta."
            });

        Validations.Of(defaultStage).Acknowledge(new Acknowledgment
        {
            Id = "AwsSolutions-APIG1",
            Reason = "Logs de acceso de API Gateway desactivados para mantenerse dentro de los 5 GB gratuitos de CloudWatch."
        });

        Validations.Of(defaultRoute).Acknowledge(new Acknowledgment
        {
            Id = "AwsSolutions-APIG4",
            Reason = "HTTP API actúa como proxy ($default) delegando autenticación JWT Bearer y políticas por ruta a Sensus.Api."
        });

        // Panel mínimo de operación (alcance de la Spec 015); CloudWatch incluye 3 paneles gratuitos.
        var dashboard = new Dashboard(this, "SensusOperationsDashboard", new DashboardProps
        {
            DashboardName = $"sensus-operations-{envName}"
        });
        dashboard.AddWidgets(
            new GraphWidget(new GraphWidgetProps
            {
                Title = "Latencia p95 (ms)",
                Left = [appLatencyMetric, apiFunction.MetricDuration(new MetricOptions { Period = Duration.Minutes(5), Statistic = "p95" })],
                Width = 12
            }),
            new GraphWidget(new GraphWidgetProps
            {
                Title = "Errores y throttling",
                Left = [appServerErrorsMetric, appThrottledMetric, apiFunction.MetricErrors(), apiFunction.MetricThrottles()],
                Width = 12
            }));

        // 6. Presupuesto Mensual en AWS Budgets con Alarma de Costo (FR-045-07, Principio VI)
        // FR-045-14: el presupuesto notifica al tema SNS; cambiar el correo ya no obliga a recrearlo. El sufijo -v2 permite el
        // reemplazo de esta única vez (de suscriptor EMAIL a SNS) sin chocar con el nombre del presupuesto existente.
        var budget = new CfnBudget(this, "SensusZeroCostBudget", new CfnBudgetProps
        {
            Budget = new CfnBudget.BudgetDataProperty
            {
                BudgetName = $"sensus-zero-cost-budget-{envName}-v2",
                BudgetType = "COST",
                TimeUnit = "MONTHLY",
                BudgetLimit = new CfnBudget.SpendProperty
                {
                    Amount = monthlyBudgetUsd,
                    Unit = "USD"
                }
            },
            NotificationsWithSubscribers = new CfnBudget.NotificationWithSubscribersProperty[]
            {
                new CfnBudget.NotificationWithSubscribersProperty
                {
                    Notification = new CfnBudget.NotificationProperty
                    {
                        NotificationType = "ACTUAL",
                        ComparisonOperator = "GREATER_THAN",
                        Threshold = 80,
                        ThresholdType = "PERCENTAGE"
                    },
                    Subscribers = new CfnBudget.SubscriberProperty[]
                    {
                        new CfnBudget.SubscriberProperty
                        {
                            SubscriptionType = "SNS",
                            Address = alarmTopic.TopicArn
                        }
                    }
                }
            }
        });
        if (budgetsPublish.PolicyDependable is not null)
        {
            budget.Node.AddDependency(budgetsPublish.PolicyDependable);
        }

        // 7. Salidas del Stack (FR-045-10)
        _ = new CfnOutput(this, "ApiGatewayUrl", new CfnOutputProps
        {
            Description = "URL HTTPS publica del API Gateway para configurar DEFAULT_BASE_URL en la app movil",
            Value = $"https://{httpApi.Ref}.execute-api.{Region}.amazonaws.com"
        });

        _ = new CfnOutput(this, "LambdaFunctionArn", new CfnOutputProps
        {
            Description = "ARN de la funcion AWS Lambda de Sensus Backend",
            Value = apiFunction.FunctionArn
        });

        _ = new CfnOutput(this, "CloudWatchLogGroupName", new CfnOutputProps
        {
            Description = "Nombre del grupo de logs en Amazon CloudWatch",
            Value = apiLogGroup.LogGroupName
        });

        _ = new CfnOutput(this, "InteractionsHmacSsmParameterPath", new CfnOutputProps
        {
            Description = "Ruta SSM SecureString para las claves HMAC versionadas de Spec 055 (rotacion <= 90 dias)",
            Value = $"/sensus/{envName}/Interactions/HmacKeysJson"
        });
    }

    private string[] ResolveAllowedOrigins()
    {
        if (Node.TryGetContext("allowedOrigins") is object[] rawOrigins && rawOrigins.Length > 0)
        {
            var origins = rawOrigins
                .Select(o => o?.ToString())
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Cast<string>()
                .ToArray();

            if (origins.Length > 0)
            {
                return origins;
            }
        }

        return
        [
            "http://localhost:3000",
            "http://localhost:5173",
            "https://18-117-247-171.sslip.io"
        ];
    }

    private static string ResolveLambdaAssetPath()
    {
        var cwd = Directory.GetCurrentDirectory();
        string[] candidatePublishPaths =
        [
            Path.Combine(cwd, "publish"),
            Path.Combine(cwd, "infra", "publish")
        ];

        foreach (var publishPath in candidatePublishPaths)
        {
            if (Directory.Exists(publishPath) && Directory.EnumerateFileSystemEntries(publishPath).Any())
            {
                return publishPath;
            }
        }

        string[] candidatePlaceholderPaths =
        [
            Path.Combine(cwd, "assets", "lambda-placeholder"),
            Path.Combine(cwd, "infra", "assets", "lambda-placeholder")
        ];

        foreach (var placeholderPath in candidatePlaceholderPaths)
        {
            if (Directory.Exists(placeholderPath))
            {
                return placeholderPath;
            }
        }

        var tempPlaceholder = Path.Combine(Path.GetTempPath(), "sensus-lambda-placeholder");
        Directory.CreateDirectory(tempPlaceholder);
        var bootstrapFile = Path.Combine(tempPlaceholder, "bootstrap");
        if (!File.Exists(bootstrapFile))
        {
            File.WriteAllText(bootstrapFile, "#!/bin/sh\necho 'Sensus Lambda placeholder'\n");
        }

        return tempPlaceholder;
    }
}
