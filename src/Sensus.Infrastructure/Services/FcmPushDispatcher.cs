using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Sensus.Application.Common.Interfaces;

namespace Sensus.Infrastructure.Services;

/// <summary>
/// Adaptador de despacho push mediante Firebase Cloud Messaging (FCM).
/// Implementación de desarrollo/prueba: simula el envío sin credenciales reales.
/// En producción se configuran las credenciales de FCM vía variables de entorno o Secret Manager.
/// </summary>
public sealed class FcmPushDispatcher : IPushDispatcher
{
    private readonly ILogger<FcmPushDispatcher> _logger;
    private static readonly HttpClient SharedHttpClient = new() { Timeout = TimeSpan.FromSeconds(10) };

    public FcmPushDispatcher(ILogger<FcmPushDispatcher> logger)
    {
        _logger = logger;
    }

    public async Task<bool> SendAsync(
        string token,
        string title,
        string body,
        IReadOnlyDictionary<string, string>? data = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "FCM dispatch (simulado): Token={TokenPrefix}..., Title={Title}, Body={Body}",
            token.Substring(0, Math.Min(8, token.Length)),
            title,
            body);

        // En desarrollo/testing sin credenciales FCM, se simula el envío exitoso.
        // En producción, se usaría el SDK de FCM o llamadas HTTP a la API de FCM.
        await Task.Delay(50, cancellationToken);
        return true;
    }

    public async Task<bool> ValidateTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("FCM token validation (simulado): Token={TokenPrefix}...", token.Substring(0, Math.Min(8, token.Length)));
        await Task.Delay(50, cancellationToken);
        // En desarrollo, cualquier token no vacío se considera válido.
        return !string.IsNullOrWhiteSpace(token) && token.Length >= 20;
    }
}
