using System.Collections.Concurrent;
using Sensus.Domain.Repositories;
using RefreshTokenEntity = Sensus.Domain.Entities.RefreshToken;

namespace Sensus.TestKit.Fakes;

/// <summary>Repositorio en memoria de refresh tokens rotatorios para pruebas sin MongoDB.</summary>
public sealed class InMemoryRefreshTokenRepository : IRefreshTokenRepository
{
    private readonly ConcurrentDictionary<Guid, RefreshTokenEntity> _tokens = new();
    private readonly object _gate = new();

    public Task AddAsync(RefreshTokenEntity token, CancellationToken cancellationToken = default)
    {
        _tokens[token.Id] = token;
        return Task.CompletedTask;
    }

    public Task<RefreshTokenEntity?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default)
    {
        RefreshTokenEntity? match = _tokens.Values.FirstOrDefault(t => string.Equals(t.TokenHash, tokenHash, StringComparison.Ordinal));
        return Task.FromResult(match);
    }

    public Task<bool> TryMarkAsUsedAsync(Guid tokenId, Guid replacedByTokenId, DateTime usedAtUtc, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if (!_tokens.TryGetValue(tokenId, out RefreshTokenEntity? token))
            {
                return Task.FromResult(false);
            }

            return Task.FromResult(token.TryMarkUsed(replacedByTokenId, usedAtUtc));
        }
    }

    public Task<int> RevokeFamilyAsync(Guid familyId, DateTime revokedAtUtc, string reason, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            int count = 0;
            foreach (RefreshTokenEntity token in _tokens.Values.Where(t => t.FamilyId == familyId && t.RevokedAtUtc is null))
            {
                token.Revoke(revokedAtUtc, reason);
                count++;
            }

            return Task.FromResult(count);
        }
    }

    public Task<int> RevokeAllByUserAsync(Guid userId, DateTime revokedAtUtc, string reason, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            int count = 0;
            foreach (RefreshTokenEntity token in _tokens.Values.Where(t => t.UserId == userId && t.RevokedAtUtc is null))
            {
                token.Revoke(revokedAtUtc, reason);
                count++;
            }

            return Task.FromResult(count);
        }
    }
}
