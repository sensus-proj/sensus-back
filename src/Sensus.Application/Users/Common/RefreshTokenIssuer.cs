using System.Security.Cryptography;
using System.Text;
using Sensus.Domain.Common;
using Sensus.Domain.Repositories;
using RefreshTokenEntity = Sensus.Domain.Entities.RefreshToken;

namespace Sensus.Application.Users.Common;

internal static class RefreshTokenIssuer
{
    public static string HashToken(string rawToken)
    {
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken.Trim()));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    public static string GenerateRawToken()
    {
        byte[] random = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(random)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }

    public static async Task<(string? RawToken, DateTime? ExpiresAtUtc, Guid TokenId)> IssueAsync(
        IRefreshTokenRepository? repository,
        Guid userId,
        DateTime nowUtc,
        int refreshTokenDays,
        Guid? familyId = null,
        CancellationToken cancellationToken = default)
    {
        if (repository is null)
        {
            return (null, null, Guid.Empty);
        }

        string rawToken = GenerateRawToken();
        string tokenHash = HashToken(rawToken);
        DateTime normalizedNow = DateTime.SpecifyKind(nowUtc, DateTimeKind.Utc);
        int boundedDays = Math.Clamp(refreshTokenDays, 1, 90);
        DateTime expiresAt = normalizedNow.AddDays(boundedDays);

        Result<RefreshTokenEntity> createResult = RefreshTokenEntity.Create(
            userId,
            tokenHash,
            normalizedNow,
            expiresAt,
            familyId);

        if (createResult.IsFailure)
        {
            return (null, null, Guid.Empty);
        }

        await repository.AddAsync(createResult.Value, cancellationToken);
        return (rawToken, expiresAt, createResult.Value.Id);
    }
}
