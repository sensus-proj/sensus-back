using Sensus.Domain.Entities;

namespace Sensus.Domain.Repositories;

/// <summary>
/// Persistencia de refresh tokens rotatorios con detección de reutilización por familia (Spec 002 / G-06).
/// </summary>
public interface IRefreshTokenRepository
{
    Task AddAsync(RefreshToken token, CancellationToken cancellationToken = default);
    Task<RefreshToken?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default);
    Task<bool> TryMarkAsUsedAsync(Guid tokenId, Guid replacedByTokenId, DateTime usedAtUtc, CancellationToken cancellationToken = default);
    Task<int> RevokeFamilyAsync(Guid familyId, DateTime revokedAtUtc, string reason, CancellationToken cancellationToken = default);
    Task<int> RevokeAllByUserAsync(Guid userId, DateTime revokedAtUtc, string reason, CancellationToken cancellationToken = default);
}
