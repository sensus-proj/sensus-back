using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Amazon.Runtime;
using Google.Apis.Auth.OAuth2.Responses;
using Microsoft.Extensions.Logging;
using Sensus.Application.Common.Interfaces;

namespace Sensus.Infrastructure.Services;

public sealed class FcmPushDispatcher : IPushDispatcher
{
    private readonly HttpClient _httpClient;
    private readonly Func<CancellationToken, Task<(string ProjectId, string AccessToken)>> _authorize;
    private readonly ILogger<FcmPushDispatcher> _logger;

    public FcmPushDispatcher(HttpClient httpClient, FcmCredentials credentials, ILogger<FcmPushDispatcher> logger)
        : this(httpClient, credentials.AuthorizeAsync, logger)
    {
    }

    internal FcmPushDispatcher(HttpClient httpClient,
        Func<CancellationToken, Task<(string ProjectId, string AccessToken)>> authorize,
        ILogger<FcmPushDispatcher> logger)
    {
        _httpClient = httpClient;
        _authorize = authorize;
        _logger = logger;
    }

    public Task<PushResult> ValidateTokenAsync(string token, CancellationToken cancellationToken = default) =>
        DispatchAsync(new { token, data = new { type = "device_validation" } }, true, cancellationToken);

    public Task<PushResult> SendAsync(
        string token,
        string title,
        string body,
        IReadOnlyDictionary<string, string>? data = null,
        CancellationToken cancellationToken = default) =>
        DispatchAsync(new { token, notification = new { title, body }, data = data ?? new Dictionary<string, string>() },
            false, cancellationToken);

    private async Task<PushResult> DispatchAsync(object message, bool validateOnly, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(2));
        try
        {
            (string projectId, string accessToken) = await _authorize(timeout.Token);
            using var request = new HttpRequestMessage(HttpMethod.Post,
                $"v1/projects/{Uri.EscapeDataString(projectId)}/messages:send");
            request.Headers.Authorization = new AuthenticationHeaderValue(
                "Bearer", accessToken);
            request.Content = JsonContent.Create(new { message, validate_only = validateOnly });
            using HttpResponseMessage response = await _httpClient.SendAsync(request, timeout.Token);
            using JsonDocument result = await response.Content.ReadFromJsonAsync<JsonDocument>(timeout.Token)
                ?? throw new JsonException("FCM returned an empty response.");

            if (response.IsSuccessStatusCode &&
                result.RootElement.TryGetProperty("name", out JsonElement name) &&
                name.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(name.GetString()))
            {
                return PushResult.Accepted;
            }

            if (response.StatusCode == HttpStatusCode.NotFound && IsUnregistered(result.RootElement))
            {
                _logger.LogInformation("FCM rejected an unregistered device");
                return PushResult.InvalidToken;
            }

            _logger.LogWarning("FCM returned an unsuccessful or incomplete response ({StatusCode})", (int)response.StatusCode);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("FCM operation timed out");
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException or TokenResponseException
            or AmazonServiceException or AmazonClientException or InvalidOperationException or ArgumentException
            or System.Security.Cryptography.CryptographicException)
        {
            _logger.LogWarning("FCM operation failed ({ErrorType})", exception.GetType().Name);
        }

        return PushResult.Unavailable;
    }

    private static bool IsUnregistered(JsonElement response) =>
        response.ValueKind == JsonValueKind.Object &&
        response.TryGetProperty("error", out JsonElement error) &&
        error.ValueKind == JsonValueKind.Object &&
        error.TryGetProperty("details", out JsonElement details) &&
        details.ValueKind == JsonValueKind.Array &&
        details.EnumerateArray().Any(detail =>
            detail.ValueKind == JsonValueKind.Object &&
            detail.TryGetProperty("@type", out JsonElement type) &&
            type.ValueKind == JsonValueKind.String &&
            type.GetString() == "type.googleapis.com/google.firebase.fcm.v1.FcmError" &&
            detail.TryGetProperty("errorCode", out JsonElement code) &&
            code.ValueKind == JsonValueKind.String && code.GetString() == "UNREGISTERED");
}
