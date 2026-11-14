using System.Globalization;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sensus.Application.Common.Interfaces;
using Sensus.Domain.Common;

namespace Sensus.Infrastructure.Services;

/// <summary>
/// Adaptador de almacenamiento multimedia en Cloudinary para subidas desde el backend (Spec 022).
/// Sube imágenes de forma privada y las expone solo tras moderación aprobada.
/// </summary>
public sealed class CloudinaryMediaStorage : IMediaStorage
{
    private static readonly HttpClient SharedHttpClient = new() { Timeout = TimeSpan.FromSeconds(30) };
    private readonly CloudinarySettings _settings;
    private readonly ILogger<CloudinaryMediaStorage> _logger;

    public CloudinaryMediaStorage(
        IOptions<CloudinarySettings> settings,
        ILogger<CloudinaryMediaStorage> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<Result<MediaUploadResult>> UploadImageAsync(
        byte[] imageBytes,
        string contentType,
        Guid ownerId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_settings.ApiKey) ||
            string.IsNullOrWhiteSpace(_settings.ApiSecret) ||
            string.IsNullOrWhiteSpace(_settings.CloudName))
        {
            _logger.LogWarning("Credenciales de Cloudinary no configuradas; se omite la subida de '{OwnerId}'.", ownerId);
            return Result.Failure<MediaUploadResult>(
                Error.Failure("Media.CloudinaryNotConfigured", "El almacenamiento multimedia no está configurado."));
        }

        try
        {
            var timestampSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var folder = $"sensus/media/{ownerId}";

            // Cadena canónica para firma en orden alfabético
            var toSign = $"folder={folder}&timestamp={timestampSeconds}{_settings.ApiSecret}";
            var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(toSign));
            var signature = Convert.ToHexString(hashBytes).ToLowerInvariant();

            using var content = new MultipartFormDataContent();
            using var fileContent = new ByteArrayContent(imageBytes);
            fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
            content.Add(fileContent, "file", $"upload-{ownerId}-{timestampSeconds}.{GetExtension(contentType)}");
            content.Add(new StringContent(_settings.ApiKey), "api_key");
            content.Add(new StringContent(timestampSeconds.ToString(CultureInfo.InvariantCulture)), "timestamp");
            content.Add(new StringContent(folder), "folder");
            content.Add(new StringContent(signature), "signature");

            var uploadUrl = $"https://api.cloudinary.com/v1_1/{_settings.CloudName}/image/upload";
            using var response = await SharedHttpClient.PostAsync(uploadUrl, content, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("Error al subir imagen a Cloudinary (HTTP {StatusCode}): {ErrorBody}", response.StatusCode, errorBody);
                return Result.Failure<MediaUploadResult>(
                    Error.Failure("Media.UploadFailed", "No se pudo subir la imagen al almacenamiento."));
            }

            var responseJson = await response.Content.ReadFromJsonAsync<CloudinaryUploadResponse>(cancellationToken);
            var publicId = responseJson?.PublicId ?? string.Empty;
            var secureUrl = responseJson?.SecureUrl;

            _logger.LogInformation("Imagen subida a Cloudinary: PublicId={PublicId}, OwnerId={OwnerId}", publicId, ownerId);

            return Result.Success(new MediaUploadResult(publicId, secureUrl));
        }
        catch (OperationCanceledException)
        {
            _logger.LogError("Timeout al subir imagen a Cloudinary para OwnerId={OwnerId}.", ownerId);
            return Result.Failure<MediaUploadResult>(
                Error.Failure("Media.UploadTimeout", "Tiempo de espera agotado al subir la imagen."));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Excepción al subir imagen a Cloudinary para OwnerId={OwnerId}.", ownerId);
            return Result.Failure<MediaUploadResult>(
                Error.Failure("Media.UploadError", "Ocurrió un error al subir la imagen."));
        }
    }

    public async Task<bool> DeleteImageAsync(string publicId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(publicId))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(_settings.ApiSecret) ||
            string.IsNullOrWhiteSpace(_settings.ApiKey) ||
            string.IsNullOrWhiteSpace(_settings.CloudName))
        {
            _logger.LogWarning("Credenciales de Cloudinary incompletas; se omite eliminación de '{PublicId}'.", publicId);
            return true;
        }

        try
        {
            var timestampSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var toSign = $"public_id={publicId}&timestamp={timestampSeconds}{_settings.ApiSecret}";
            var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(toSign));
            var signature = Convert.ToHexString(hashBytes).ToLowerInvariant();

            using var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["public_id"] = publicId,
                ["api_key"] = _settings.ApiKey,
                ["timestamp"] = timestampSeconds.ToString(CultureInfo.InvariantCulture),
                ["signature"] = signature
            });

            var destroyUrl = $"https://api.cloudinary.com/v1_1/{_settings.CloudName}/image/destroy";
            using var response = await SharedHttpClient.PostAsync(destroyUrl, content, cancellationToken);
            _logger.LogInformation("Imagen '{PublicId}' eliminada de Cloudinary (HTTP {StatusCode}).", publicId, (int)response.StatusCode);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error al intentar eliminar la imagen '{PublicId}' de Cloudinary.", publicId);
            return false;
        }
    }

    private static string GetExtension(string contentType)
    {
        return contentType.Trim().ToLowerInvariant() switch
        {
            "image/jpeg" => "jpg",
            "image/png" => "png",
            "image/webp" => "webp",
            _ => "bin"
        };
    }

    private sealed class CloudinaryUploadResponse
    {
        public string? PublicId { get; set; }
        public string? SecureUrl { get; set; }
    }
}
