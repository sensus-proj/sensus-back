using System.Net;
using System.Net.Http.Headers;
using Microsoft.Extensions.Logging;

namespace Sensus.Infrastructure.Services;

/// <summary>
/// Ejecuta solicitudes HTTP de moderación con reintentos acotados ante fallos transitorios
/// (<c>HttpRequestException</c>, 429, 500, 502, 503 y 504). No reintenta cancelación, timeout ni JSON malformado.
/// </summary>
internal static class ModerationHttpRetryHelper
{
    private const int MaxRetryAfterMilliseconds = 2000;

    internal static async Task<HttpResponseMessage> SendWithRetryAsync(
        HttpClient httpClient,
        Func<HttpRequestMessage> requestFactory,
        ModerationRetrySettings? retrySettings,
        int timeoutSeconds,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var maxRetries = Math.Clamp(retrySettings?.MaxRetries ?? 1, 0, 2);
        var defaultDelayMs = Math.Clamp(retrySettings?.RetryDelayMilliseconds ?? 500, 0, MaxRetryAfterMilliseconds);
        var effectiveTimeoutSeconds = timeoutSeconds > 0 ? timeoutSeconds : 8;

        for (var attempt = 0; ; attempt++)
        {
            int delayMs;
            try
            {
                using var request = requestFactory();
                using var attemptCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                attemptCts.CancelAfter(TimeSpan.FromSeconds(effectiveTimeoutSeconds));

                var response = await httpClient.SendAsync(request, attemptCts.Token);
                if (!IsTransientStatusCode(response.StatusCode) || attempt >= maxRetries)
                {
                    return response;
                }

                delayMs = ResolveDelayMilliseconds(response.Headers.RetryAfter, defaultDelayMs);
                logger.LogWarning(
                    "Respuesta HTTP transitoria {StatusCode} en moderación (intento {Attempt}/{Total}); reintento en {DelayMs} ms.",
                    (int)response.StatusCode,
                    attempt + 1,
                    maxRetries + 1,
                    delayMs);
                response.Dispose();
            }
            catch (HttpRequestException ex) when (attempt < maxRetries)
            {
                delayMs = defaultDelayMs;
                logger.LogWarning(
                    ex,
                    "Error de red transitorio en moderación (intento {Attempt}/{Total}); reintento en {DelayMs} ms.",
                    attempt + 1,
                    maxRetries + 1,
                    delayMs);
            }

            if (delayMs > 0)
            {
                await Task.Delay(delayMs, cancellationToken);
            }
        }
    }

    private static bool IsTransientStatusCode(HttpStatusCode statusCode) =>
        statusCode is HttpStatusCode.TooManyRequests
            or HttpStatusCode.InternalServerError
            or HttpStatusCode.BadGateway
            or HttpStatusCode.ServiceUnavailable
            or HttpStatusCode.GatewayTimeout;

    private static int ResolveDelayMilliseconds(RetryConditionHeaderValue? retryAfter, int defaultDelayMs)
    {
        if (retryAfter?.Delta is { } delta)
        {
            if (delta <= TimeSpan.Zero)
            {
                return 0;
            }

            return delta <= TimeSpan.FromMilliseconds(MaxRetryAfterMilliseconds)
                ? (int)Math.Ceiling(delta.TotalMilliseconds)
                : defaultDelayMs;
        }

        if (retryAfter?.Date is { } date)
        {
            var deltaFromDate = date - DateTimeOffset.UtcNow;
            if (deltaFromDate <= TimeSpan.Zero)
            {
                return 0;
            }

            return deltaFromDate <= TimeSpan.FromMilliseconds(MaxRetryAfterMilliseconds)
                ? (int)Math.Ceiling(deltaFromDate.TotalMilliseconds)
                : defaultDelayMs;
        }

        return defaultDelayMs;
    }
}
