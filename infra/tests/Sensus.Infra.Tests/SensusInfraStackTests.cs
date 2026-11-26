using Amazon.CDK;
using Amazon.CDK.Assertions;
using Cdklabs.CdkNag;

namespace Sensus.Infra.Tests;

public sealed class SensusInfraStackTests
{
    private static string ResolveRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "infra", "assets", "night-switch")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("No se encontró la raíz del repositorio con infra/assets/night-switch.");
    }

    [Fact]
    public void DefaultHttpApiStage_ConfiguresThrottlingDefaultsAndPassesCdkNag()
    {
        var previousCwd = Directory.GetCurrentDirectory();
        var repoRoot = ResolveRepoRoot();
        try
        {
            Directory.SetCurrentDirectory(repoRoot);
            var app = new App();
            var stack = new SensusInfraStack(app, "TestStack", new StackProps
            {
                Env = new Amazon.CDK.Environment
                {
                    Account = "123456789012",
                    Region = "us-east-2"
                }
            });

            Validations.Of(app).AddPlugins(new AwsSolutionsChecks(app, new NagPackProps { Verbose = true }));
            var assembly = app.Synth();
            Assert.NotNull(assembly);

            var template = Template.FromStack(stack);
            template.HasResourceProperties("AWS::ApiGatewayV2::Stage", new Dictionary<string, object>
            {
                ["StageName"] = "$default",
                ["DefaultRouteSettings"] = new Dictionary<string, object>
                {
                    ["ThrottlingRateLimit"] = 50,
                    ["ThrottlingBurstLimit"] = 100
                }
            });
        }
        finally
        {
            Directory.SetCurrentDirectory(previousCwd);
        }
    }

    [Fact]
    public void DefaultHttpApiStage_ReadsThrottlingFromContext()
    {
        var previousCwd = Directory.GetCurrentDirectory();
        var repoRoot = ResolveRepoRoot();
        try
        {
            Directory.SetCurrentDirectory(repoRoot);
            var app = new App(new AppProps
            {
                Context = new Dictionary<string, object>
                {
                    ["apiThrottleRate"] = "75",
                    ["apiThrottleBurst"] = "150"
                }
            });
            var stack = new SensusInfraStack(app, "TestCustomThrottleStack", new StackProps
            {
                Env = new Amazon.CDK.Environment
                {
                    Account = "123456789012",
                    Region = "us-east-2"
                }
            });

            var template = Template.FromStack(stack);
            template.HasResourceProperties("AWS::ApiGatewayV2::Stage", new Dictionary<string, object>
            {
                ["StageName"] = "$default",
                ["DefaultRouteSettings"] = new Dictionary<string, object>
                {
                    ["ThrottlingRateLimit"] = 75,
                    ["ThrottlingBurstLimit"] = 150
                }
            });
        }
        finally
        {
            Directory.SetCurrentDirectory(previousCwd);
        }
    }
}
