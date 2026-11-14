using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Sensus.Application.Common.Interfaces;

namespace Sensus.Infrastructure.Services;

/// <summary>
/// Firma y valida los códigos de apartado con HMAC-SHA256 y comparación en tiempo constante
/// (FR-033-04, FR-033-08). Solo se persiste el hash SHA-256 del código.
/// </summary>
public sealed class HmacClaimCodeService : IClaimCodeService
{
    private const string Version = "v1";
    private readonly byte[] _key;

    public HmacClaimCodeService(IOptions<ClaimCodeSettings> settings)
    {
        string secret = settings.Value.Secret;
        if (string.IsNullOrWhiteSpace(secret))
        {
            throw new InvalidOperationException(
                "ClaimCodeSettings:Secret no está configurado. Defínalo por variable de entorno o AWS Secrets Manager.");
        }

        _key = Encoding.UTF8.GetBytes(secret);
    }

    public ClaimCode Generate(Guid claimId, Guid offerId, DateTime expiresAtUtc)
    {
        long expiresAtUnix = new DateTimeOffset(expiresAtUtc.ToUniversalTime()).ToUnixTimeSeconds();
        string payload = string.Join(
            '.',
            Version,
            claimId.ToString("N", CultureInfo.InvariantCulture),
            offerId.ToString("N", CultureInfo.InvariantCulture),
            expiresAtUnix.ToString(CultureInfo.InvariantCulture));

        string code = $"{payload}.{Sign(payload)}";
        return new ClaimCode(code, ComputeHash(code), code);
    }

    public string ComputeHash(string code)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(code));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public bool TryValidate(string code, DateTime nowUtc, out ClaimCodePayload payload)
    {
        payload = new ClaimCodePayload(Guid.Empty, Guid.Empty, default);

        if (string.IsNullOrWhiteSpace(code))
        {
            return false;
        }

        string[] parts = code.Split('.');
        if (parts.Length != 5 || !string.Equals(parts[0], Version, StringComparison.Ordinal))
        {
            return false;
        }

        if (!Guid.TryParseExact(parts[1], "N", out Guid claimId) ||
            !Guid.TryParseExact(parts[2], "N", out Guid offerId) ||
            !long.TryParse(parts[3], NumberStyles.None, CultureInfo.InvariantCulture, out long expiresAtUnix))
        {
            return false;
        }

        string signedPayload = string.Join('.', parts[0], parts[1], parts[2], parts[3]);
        byte[] expected = Encoding.UTF8.GetBytes(Sign(signedPayload));
        byte[] provided = Encoding.UTF8.GetBytes(parts[4]);

        if (expected.Length != provided.Length ||
            !CryptographicOperations.FixedTimeEquals(expected, provided))
        {
            return false;
        }

        DateTime expiresAtUtc = DateTimeOffset.FromUnixTimeSeconds(expiresAtUnix).UtcDateTime;
        if (nowUtc >= expiresAtUtc)
        {
            return false;
        }

        payload = new ClaimCodePayload(claimId, offerId, expiresAtUtc);
        return true;
    }

    private string Sign(string payload)
    {
        byte[] signature = HMACSHA256.HashData(_key, Encoding.UTF8.GetBytes(payload));
        return Convert.ToBase64String(signature)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }
}
