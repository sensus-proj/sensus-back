using Amazon.SimpleSystemsManagement;
using Amazon.SimpleSystemsManagement.Model;
using Google.Apis.Auth.OAuth2;
using Microsoft.Extensions.Configuration;

namespace Sensus.Infrastructure.Services;

public sealed class FcmCredentials(
    IAmazonSimpleSystemsManagement parameterStore,
    IConfiguration configuration) : IDisposable
{
    private const string Scope = "https://www.googleapis.com/auth/firebase.messaging";
    private readonly SemaphoreSlim _initialization = new(1);
    private volatile ServiceAccountCredential? _credential;

    public async Task<(string ProjectId, string AccessToken)> AuthorizeAsync(CancellationToken cancellationToken)
    {
        ServiceAccountCredential? credential = _credential;
        if (credential is null)
        {
            await _initialization.WaitAsync(cancellationToken);
            try
            {
                credential = _credential;
                if (credential is null)
                {
                    string? prefix = configuration["AWS_SSM_PARAMETER_PREFIX"];
                    if (string.IsNullOrWhiteSpace(prefix))
                    {
                        throw new InvalidOperationException("AWS parameter prefix is missing.");
                    }

                    GetParameterResponse response = await parameterStore.GetParameterAsync(new GetParameterRequest
                    {
                        Name = $"{prefix.TrimEnd('/')}/push/fcm-service-account",
                        WithDecryption = true
                    }, cancellationToken);
                    credential = CredentialFactory.FromJson<ServiceAccountCredential>(response.Parameter.Value);
                    credential = (ServiceAccountCredential)credential.ToGoogleCredential().CreateScoped(Scope).UnderlyingCredential;
                    _credential = credential;
                }
            }
            finally
            {
                _initialization.Release();
            }
        }

        string projectId = credential.ProjectId ?? throw new InvalidOperationException("FCM project is missing.");
        string accessToken = await credential.GetAccessTokenForRequestAsync(cancellationToken: cancellationToken);
        return (projectId, accessToken);
    }

    public void Dispose() => _initialization.Dispose();
}
