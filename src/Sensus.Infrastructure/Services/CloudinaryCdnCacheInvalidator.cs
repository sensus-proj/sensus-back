using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sensus.Application.Common.Interfaces;

namespace Sensus.Infrastructure.Services;

/// <summary>
/// Invalida la caché de Cloudinary cuando un activo se retira de circulación (Spec 023).
/// La operación es de mejor esfuerzo: un fallo del CDN no revierte la decisión de moderación.
/// Ref: FR-023-04 - invalidar caché al retirar contenido.
/// </summary>
public sealed class CloudinaryCdnCacheInvalidator : ICdnCacheInvalidator
{
    private static readonly HttpClient SharedHttpClient = new() { Timeout = TimeSpan.FromSeconds(15) };

    private readonly HttpClient _httpClient;
    private readonly CloudinarySettings _settings;
    private readonly ILogger<CloudinaryCdnCacheInvalidator> _logger;

    public CloudinaryCdnCacheInvalidator(
        IOptions<CloudinarySettings> settings,
        ILogger<CloudinaryCdnCacheInvalidator> logger)
        : this(SharedHttpClient, settings, logger)
    {
    }

    internal CloudinaryCdnCacheInvalidator(
        HttpClient httpClient,
        IOptions<CloudinarySettings> settings,
        ILogger<CloudinaryCdnCacheInvalidator> logger)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<bool> InvalidateAsync(string publicId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(publicId))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(_settings.ApiKey)
            || string.IsNullOrWhiteSpace(_settings.ApiSecret)
            || string.IsNullOrWhiteSpace(_settings.CloudName))
        {
            _logger.LogWarning("Credenciales de Cloudinary incompletas; se omite la invalidación de '{PublicId}'.", publicId);
            return false;
        }

        try
        {
            long timestampSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            string toSign = $"public_ids[]={publicId}&timestamp={timestampSeconds}{_settings.ApiSecret}";
            byte[] hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(toSign));
            string signature = Convert.ToHexString(hashBytes).ToLowerInvariant();

            using FormUrlEncodedContent content = new(new Dictionary<string, string>
            {
                ["public_ids[]"] = publicId,
                ["api_key"] = _settings.ApiKey,
                ["timestamp"] = timestampSeconds.ToString(CultureInfo.InvariantCulture),
                ["signature"] = signature
            });

            string invalidateUrl = $"https://api.cloudinary.com/v1_1/{_settings.CloudName}/invalidate";
            using HttpResponseMessage response = await _httpClient.PostAsync(invalidateUrl, content, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Cloudinary rechazó la invalidación de '{PublicId}' (HTTP {StatusCode}).",
                    publicId,
                    (int)response.StatusCode);
                return false;
            }

            _logger.LogInformation("Caché CDN invalidada para '{PublicId}'.", publicId);
            return true;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo invalidar la caché CDN de '{PublicId}'.", publicId);
            return false;
        }
    }
}
