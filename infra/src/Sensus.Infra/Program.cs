using Amazon.CDK;

namespace Sensus.Infra;

internal static class Program
{
    public static void Main()
    {
        var app = new App();

        var envName = app.Node.TryGetContext("environment") as string ?? "prod";
        var account = System.Environment.GetEnvironmentVariable("CDK_DEFAULT_ACCOUNT");
        var region = System.Environment.GetEnvironmentVariable("CDK_DEFAULT_REGION") ?? "us-east-1";

        var stack = new SensusInfraStack(app, $"SensusBackendStack-{envName}", new StackProps
        {
            Description = "Sensus Backend Serverless Infrastructure (API Gateway HTTP API v2 + AWS Lambda .NET 10 ARM64 + CloudWatch + Budgets — Spec 045)",
            Env = new Amazon.CDK.Environment
            {
                Account = account,
                Region = region
            }
        });

        // Etiquetado obligatorio según constitution.md Principio VI (FR-045-09)
        Tags.Of(stack).Add("project", "sensus");
        Tags.Of(stack).Add("environment", envName);
        Tags.Of(stack).Add("owner", "sensus-team");

        app.Synth();
    }
}
