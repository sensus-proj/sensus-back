using Amazon.CDK;
using Amazon.CDK.AWS.Apigatewayv2;
using Amazon.CDK.AWS.Budgets;
using Amazon.CDK.AWS.CloudWatch;
using Amazon.CDK.AWS.IAM;
using Amazon.CDK.AWS.Lambda;
using Amazon.CDK.AWS.Logs;
using Constructs;

namespace Sensus.Infra;

/// <summary>
/// Stack de Infraestructura Serverless de Costo Cero para Sensus Backend (Spec 045 — Principio VI).
/// Aprovisiona AWS Lambda (.NET 10 en Amazon Linux 2023 ARM64), AWS API Gateway v2 (HTTP API),
/// Amazon CloudWatch Logs (7 días de retención), alarmas operativas, acceso a SSM Parameter Store
/// y alarma presupuestaria en AWS Budgets.
/// </summary>
public sealed class SensusInfraStack : Stack
{
    public SensusInfraStack(Construct scope, string id, IStackProps? props = null)
        : base(scope, id, props)
    {
        var envName = (Node.TryGetContext("environment") as string ?? "prod").ToLowerInvariant();
        var budgetEmail = Node.TryGetContext("budgetEmail") as string ?? "alerts@sensus.local";
        var allowedOrigins = ResolveAllowedOrigins();

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
            Description = "Rol IAM de minimo privilegio para Sensus.Api Lambda (CloudWatch Logs + SSM Parameter Store)"
        });

        lambdaRole.AddToPolicy(new PolicyStatement(new PolicyStatementProps
        {
            Sid = "AllowCloudWatchLogs",
            Effect = Effect.ALLOW,
            Actions = ["logs:CreateLogStream", "logs:PutLogEvents"],
            Resources = [apiLogGroup.LogGroupArn, $"{apiLogGroup.LogGroupArn}:*"]
        }));

        // Lectura de parámetros y secretos desde AWS Systems Manager (SSM) Parameter Store (100 % gratuito, 0 USD)
        lambdaRole.AddToPolicy(new PolicyStatement(new PolicyStatementProps
        {
            Sid = "AllowSsmParameterStoreRead",
            Effect = Effect.ALLOW,
            Actions = ["ssm:GetParameter", "ssm:GetParameters", "ssm:GetParametersByPath"],
            Resources = [$"arn:aws:ssm:{Region}:{Account}:parameter/sensus/{envName}/*"]
        }));

        // 3. Función AWS Lambda para .NET 10 sobre Amazon Linux 2023 ARM64 / Graviton2 (FR-045-03)
        // Sin VPC ni NAT Gateway para garantizar costo 0.00 USD y salida directa a MongoDB Atlas, Cloudinary y OpenAI.
        var assetPath = ResolveLambdaAssetPath();

        var apiFunction = new Function(this, "SensusApiFunction", new FunctionProps
        {
            FunctionName = $"sensus-api-{envName}",
            Description = "Sensus Backend RESTful API (.NET 10 LTS Serverless sobre Amazon Linux 2023 ARM64)",
            Runtime = Runtime.PROVIDED_AL2023,
            Architecture = Architecture.ARM_64,
            Handler = "bootstrap",
            Code = Code.FromAsset(assetPath),
            Role = lambdaRole,
            MemorySize = 512,
            Timeout = Duration.Seconds(29),
            LogGroup = apiLogGroup,
            Environment = new Dictionary<string, string>
            {
                ["ASPNETCORE_ENVIRONMENT"] = "Production",
                ["AWS_SSM_PARAMETER_PREFIX"] = $"/sensus/{envName}",
                ["Redis__Enabled"] = "false",
                ["DOTNET_SYSTEM_GLOBALIZATION_INVARIANT"] = "true"
            }
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
        _ = new Alarm(this, "SensusLambdaErrorsAlarm", new AlarmProps
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

        _ = new Alarm(this, "SensusLambdaThrottlesAlarm", new AlarmProps
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

        _ = new Alarm(this, "SensusApi5xxAlarm", new AlarmProps
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

        // 6. Presupuesto Mensual en AWS Budgets con Alarma de Costo (FR-045-07, Principio VI)
        _ = new CfnBudget(this, "SensusZeroCostBudget", new CfnBudgetProps
        {
            Budget = new CfnBudget.BudgetDataProperty
            {
                BudgetName = $"sensus-zero-cost-budget-{envName}",
                BudgetType = "COST",
                TimeUnit = "MONTHLY",
                BudgetLimit = new CfnBudget.SpendProperty
                {
                    Amount = 1.0,
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
                            SubscriptionType = "EMAIL",
                            Address = budgetEmail
                        }
                    }
                }
            }
        });

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
