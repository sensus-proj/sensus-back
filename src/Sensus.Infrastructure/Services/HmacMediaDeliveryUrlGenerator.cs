using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Common.Settings;
using Sensus.Domain.Common;
using Sensus.Domain.Entities;

namespace Sensus.Infrastructure.Services;

/// <summary>
/// Emite y valida URLs de entrega firmadas con HMAC-SHA256 (Spec 023).
/// La firma cubre activo, vencimiento y alcance de privacidad, de modo que una firma pública deja de
/// servir en cuanto el activo pasa a cuarentena o se retira (fail-closed).
/// Ref: FR-023-02 - firmar URLs cuando el contenido sea privado.
/// </summary>
public sealed class HmacMediaDeliveryUrlGenerator : IMediaDeliveryUrlGenerator
{
    /// <summary>Longitud mínima exigida a la clave HMAC, en bytes.</summary>
    public const int MinimumKeyLength = 32;

    private const string DeliveryPath = "/api/v1/media";

    private readonly MediaDeliverySettings _settings;
    private readonly IDateTimeProvider _clock;
    private readonly ILogger<HmacMediaDeliveryUrlGenerator> _logger;
    private readonly byte[] _key;

    public HmacMediaDeliveryUrlGenerator(
        IOptions<MediaDeliverySettings> settings,
        IDateTimeProvider clock,
        ILogger<HmacMediaDeliveryUrlGenerator> logger)
    {
        _settings = settings.Value;
        _clock = clock;
        _logger = logger;
        _key = DecodeKey(_settings.SigningKey);
    }

    /// <inheritdoc />
    public Result<MediaDeliveryUrl> Generate(MediaAsset asset, TimeSpan ttl)
    {
        ArgumentNullException.ThrowIfNull(asset);

        if (_key.Length < MinimumKeyLength)
        {
            _logger.LogError("MediaDelivery:SigningKey no está configurada o es demasiado corta; se rechaza la emisión de URLs.");
            return Result.Failure<MediaDeliveryUrl>(
                Error.Unavailable("MediaDelivery.NotConfigured", "La entrega de contenido no está configurada."));
        }

        if (string.IsNullOrWhiteSpace(asset.SecureUrl))
        {
            return Result.Failure<MediaDeliveryUrl>(
                Error.Unavailable("MediaDelivery.ContentUnavailable", "El contenido multimedia no está disponible."));
        }

        if (ttl <= TimeSpan.Zero)
        {
            return Result.Failure<MediaDeliveryUrl>(
                Error.Validation("MediaDelivery.TtlInvalid", "La vigencia de la URL firmada debe ser positiva."));
        }

        MediaDeliveryScope scope = asset.Status == MediaAssetStatus.Approved
            ? MediaDeliveryScope.Public
            : MediaDeliveryScope.Private;

        DateTime expiresAtUtc = _clock.UtcNow.Add(ttl);
        long expiresUnixSeconds = new DateTimeOffset(expiresAtUtc).ToUnixTimeSeconds();
        string signature = Sign(asset.Id, expiresUnixSeconds, scope);
        string baseUrl = string.IsNullOrWhiteSpace(_settings.PublicBaseUrl)
            ? "https://localhost:5001"
            : _settings.PublicBaseUrl.TrimEnd('/');

        string url = string.Create(
            CultureInfo.InvariantCulture,
            $"{baseUrl}{DeliveryPath}/{asset.Id}/delivery?expires={expiresUnixSeconds}&signature={signature}");

        return Result.Success(new MediaDeliveryUrl(url, expiresAtUtc, scope));
    }

    /// <inheritdoc />
    public bool Validate(Guid mediaId, long expiresUnixSeconds, string? signature, MediaDeliveryScope scope)
    {
        if (_key.Length < MinimumKeyLength || string.IsNullOrWhiteSpace(signature))
        {
            return false;
        }

        string expected = Sign(mediaId, expiresUnixSeconds, scope);
        byte[] expectedBytes = Encoding.ASCII.GetBytes(expected);
        byte[] providedBytes = Encoding.ASCII.GetBytes(signature.Trim());

        return expectedBytes.Length == providedBytes.Length
            && CryptographicOperations.FixedTimeEquals(expectedBytes, providedBytes);
    }

    private string Sign(Guid mediaId, long expiresUnixSeconds, MediaDeliveryScope scope)
    {
        string payload = string.Create(
            CultureInfo.InvariantCulture,
            $"{mediaId:N}|{expiresUnixSeconds}|{scope}");

        byte[] hash = HMACSHA256.HashData(_key, Encoding.UTF8.GetBytes(payload));
        return Base64UrlEncode(hash);
    }

    private static string Base64UrlEncode(byte[] value) =>
        Convert.ToBase64String(value)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    private static byte[] DecodeKey(string? configuredKey)
    {
        if (string.IsNullOrWhiteSpace(configuredKey))
        {
            return [];
        }

        try
        {
            return Convert.FromBase64String(configuredKey.Trim());
        }
        catch (FormatException)
        {
            // Se acepta una clave en texto plano solo si alcanza la longitud mínima exigida.
            return Encoding.UTF8.GetBytes(configuredKey.Trim());
        }
    }
}
