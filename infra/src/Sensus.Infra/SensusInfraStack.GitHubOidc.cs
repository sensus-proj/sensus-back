using Amazon.CDK;
using Amazon.CDK.AWS.IAM;

namespace Sensus.Infra;

/// <summary>
/// Proveedor OIDC de GitHub Actions y roles de mínimo privilegio para CI/CD sin llaves largas (Spec 045, FR-045-20).
/// </summary>
public sealed partial class SensusInfraStack
{
    private static readonly string[] DeployOidcSubs =
    [
        "repo:sensus-proj/sensus-back:environment:production",
        "repo:sensus-proj/sensus-back:ref:refs/heads/main"
    ];

    private static readonly string[] RollbackPrefixFilter = ["rollback/*"];

    private static readonly string[] IacReadOnlyOidcSubs =
    [
        "repo:sensus-proj/sensus-back:pull_request",
        "repo:sensus-proj/sensus-back:ref:refs/heads/*"
    ];

    private void CreateGitHubOidcRoles(string envName)
    {
        var existingProviderArn = Node.TryGetContext("existingGithubOidcProviderArn") as string;
        IOpenIdConnectProvider githubProvider = !string.IsNullOrWhiteSpace(existingProviderArn)
            ? OpenIdConnectProvider.FromOpenIdConnectProviderArn(this, "GitHubOidcProvider", existingProviderArn.Trim())
            : new OpenIdConnectProvider(this, "GitHubOidcProvider", new OpenIdConnectProviderProps
            {
                Url = "https://token.actions.githubusercontent.com",
                ClientIds = ["sts.amazonaws.com"]
            });

        var deployRole = new Role(this, "SensusGitHubDeployRole", new RoleProps
        {
            RoleName = $"sensus-github-deploy-{envName}",
            Description = "Rol OIDC de minimo privilegio para despliegue serverless desde environment production / main (FR-045-20)",
            MaxSessionDuration = Duration.Hours(1),
            AssumedBy = new WebIdentityPrincipal(
                githubProvider.OpenIdConnectProviderArn,
                new Dictionary<string, object>
                {
                    ["StringEquals"] = new Dictionary<string, string>
                    {
                        ["token.actions.githubusercontent.com:aud"] = "sts.amazonaws.com"
                    },
                    ["StringLike"] = new Dictionary<string, object>
                    {
                        ["token.actions.githubusercontent.com:sub"] = DeployOidcSubs
                    }
                })
        });

        // Bootstrap roles necesarios para cdk deploy (cloudformation deploy / file publishing / lookup).
        deployRole.AddToPolicy(new PolicyStatement(new PolicyStatementProps
        {
            Sid = "AllowCdkBootstrapAssume",
            Effect = Effect.ALLOW,
            Actions = ["sts:AssumeRole"],
            Resources = [$"arn:aws:iam::{Account}:role/cdk-hnb659fds-*-{Account}-{Region}"]
        }));

        // Lectura/escritura acotada al prefijo rollback/ en el bucket de assets de CDK (FR-045-17).
        deployRole.AddToPolicy(new PolicyStatement(new PolicyStatementProps
        {
            Sid = "AllowRollbackAssetPrefix",
            Effect = Effect.ALLOW,
            Actions = ["s3:GetObject", "s3:PutObject"],
            Resources = [$"arn:aws:s3:::cdk-hnb659fds-assets-{Account}-{Region}/rollback/*"]
        }));

        deployRole.AddToPolicy(new PolicyStatement(new PolicyStatementProps
        {
            Sid = "AllowRollbackAssetBucketList",
            Effect = Effect.ALLOW,
            Actions = ["s3:ListBucket"],
            Resources = [$"arn:aws:s3:::cdk-hnb659fds-assets-{Account}-{Region}"],
            Conditions = new Dictionary<string, object>
            {
                ["StringLike"] = new Dictionary<string, object>
                {
                    ["s3:prefix"] = RollbackPrefixFilter
                }
            }
        }));

        // Consulta y restauracion del codigo de la Lambda sensus-api-<env> durante rollback/smoke check.
        deployRole.AddToPolicy(new PolicyStatement(new PolicyStatementProps
        {
            Sid = "AllowApiLambdaCodeOperations",
            Effect = Effect.ALLOW,
            Actions = ["lambda:GetFunction", "lambda:GetFunctionConcurrency", "lambda:UpdateFunctionCode"],
            Resources = [$"arn:aws:lambda:{Region}:{Account}:function:sensus-api-{envName}"]
        }));

        deployRole.AddToPolicy(new PolicyStatement(new PolicyStatementProps
        {
            Sid = "AllowStackOutputsRead",
            Effect = Effect.ALLOW,
            Actions = ["cloudformation:DescribeStacks"],
            Resources = [$"arn:aws:cloudformation:{Region}:{Account}:stack/SensusBackendStack-{envName}/*"]
        }));

        // sts:GetCallerIdentity no admite restriccion por ARN en AWS IAM.
        deployRole.AddToPolicy(new PolicyStatement(new PolicyStatementProps
        {
            Sid = "AllowCallerIdentity",
            Effect = Effect.ALLOW,
            Actions = ["sts:GetCallerIdentity"],
            Resources = ["*"]
        }));

        var iacReadOnlyRole = new Role(this, "SensusGitHubIacReadOnlyRole", new RoleProps
        {
            RoleName = $"sensus-github-iac-readonly-{envName}",
            Description = "Rol OIDC de solo lectura para ejecutar cdk diff en Pull Requests sin llaves largas (FR-045-20)",
            MaxSessionDuration = Duration.Hours(1),
            ManagedPolicies = [ManagedPolicy.FromAwsManagedPolicyName("ReadOnlyAccess")],
            AssumedBy = new WebIdentityPrincipal(
                githubProvider.OpenIdConnectProviderArn,
                new Dictionary<string, object>
                {
                    ["StringEquals"] = new Dictionary<string, string>
                    {
                        ["token.actions.githubusercontent.com:aud"] = "sts.amazonaws.com"
                    },
                    ["StringLike"] = new Dictionary<string, object>
                    {
                        ["token.actions.githubusercontent.com:sub"] = IacReadOnlyOidcSubs
                    }
                })
        });

        iacReadOnlyRole.AddToPolicy(new PolicyStatement(new PolicyStatementProps
        {
            Sid = "AllowCdkLookupAssume",
            Effect = Effect.ALLOW,
            Actions = ["sts:AssumeRole"],
            Resources = [$"arn:aws:iam::{Account}:role/cdk-hnb659fds-lookup-role-{Account}-{Region}"]
        }));

        Validations.Of(deployRole).Acknowledge(new Acknowledgment
        {
            Id = "AwsSolutions-IAM5",
            Reason = "Necesario para asumir roles CDK bootstrap (cdk-hnb659fds-*), prefijo rollback/* en S3, stackArn/* y sts:GetCallerIdentity (* requerido por AWS STS)."
        });

        Validations.Of(iacReadOnlyRole).Acknowledge(new Acknowledgment
        {
            Id = "AwsSolutions-IAM4",
            Reason = "ReadOnlyAccess administrado exclusivamente de solo lectura para cdk diff en Pull Requests."
        });

        _ = new CfnOutput(this, "GitHubDeployRoleArn", new CfnOutputProps
        {
            Description = "ARN del rol OIDC para configurar el secreto AWS_DEPLOY_ROLE_ARN en GitHub Actions",
            Value = deployRole.RoleArn
        });

        _ = new CfnOutput(this, "GitHubIacReadOnlyRoleArn", new CfnOutputProps
        {
            Description = "ARN del rol OIDC de solo lectura para configurar AWS_IAC_ROLE_ARN en el job iac de CI",
            Value = iacReadOnlyRole.RoleArn
        });
    }
}
