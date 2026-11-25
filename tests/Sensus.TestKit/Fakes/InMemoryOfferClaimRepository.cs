using Sensus.Domain.Entities;
using Sensus.Domain.Enums;
using Sensus.Domain.Models;
using Sensus.Domain.Repositories;

namespace Sensus.TestKit.Fakes;

/// <summary>Repositorio en memoria de apartados para pruebas sin MongoDB.</summary>
public sealed class InMemoryOfferClaimRepository : IOfferClaimRepository
{
    private readonly List<OfferClaim> _claims = [];

    public Task<OfferClaim?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_claims.FirstOrDefault(c => c.Id == id));

    public Task<int> GetActiveClaimCountAsync(Guid offerId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_claims.Count(c => c.OfferId == offerId && c.Status == OfferClaimStatus.Active));

    public Task<IReadOnlyDictionary<Guid, OfferClaimCounts>> CountByOfferIdsAsync(
        IEnumerable<Guid> offerIds,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyDictionary<Guid, OfferClaimCounts> counts = _claims
            .GroupBy(c => c.OfferId)
            .ToDictionary(
                g => g.Key,
                g => new OfferClaimCounts(
                    g.Count(c => c.Status == OfferClaimStatus.Active),
                    g.Count(c => c.Status == OfferClaimStatus.Redeemed)));
        return Task.FromResult(counts);
    }

    public Task<OfferClaim?> GetActiveByUserAndOfferAsync(
        Guid userId,
        Guid offerId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(_claims.FirstOrDefault(c =>
            c.UserId == userId &&
            c.OfferId == offerId &&
            c.Status == OfferClaimStatus.Active));

    public Task<OfferClaim?> GetByCodeHashAsync(string codeHash, CancellationToken cancellationToken = default) =>
        Task.FromResult(_claims.FirstOrDefault(c => c.CodeHash == codeHash));

    public Task<bool> TryAddAsync(OfferClaim claim, CancellationToken cancellationToken = default)
    {
        if (_claims.Any(c =>
                c.UserId == claim.UserId &&
                c.OfferId == claim.OfferId &&
                c.Status == OfferClaimStatus.Active))
        {
            return Task.FromResult(false);
        }

        _claims.Add(claim);
        return Task.FromResult(true);
    }

    public Task UpdateAsync(OfferClaim claim, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task<bool> TryRedeemAsync(
        Guid claimId,
        Guid merchantId,
        DateTime nowUtc,
        CancellationToken cancellationToken = default)
    {
        OfferClaim? claim = _claims.FirstOrDefault(c => c.Id == claimId);
        if (claim is null || claim.MerchantId != merchantId)
        {
            return Task.FromResult(false);
        }

        return Task.FromResult(claim.Redeem(merchantId, nowUtc).IsSuccess);
    }

    public Task<IReadOnlyList<OfferClaim>> ExpireClaimsAsync(
        DateTime nowUtc,
        CancellationToken cancellationToken = default)
    {
        var expired = new List<OfferClaim>();
        foreach (OfferClaim claim in _claims.Where(c => c.Status == OfferClaimStatus.Active))
        {
            if (claim.Expire(nowUtc).IsSuccess)
            {
                expired.Add(claim);
            }
        }

        return Task.FromResult<IReadOnlyList<OfferClaim>>(expired);
    }

    public Task<IReadOnlyList<OfferClaim>> GetByUserIdAsync(
        Guid userId,
        OfferClaimStatus? status = null,
        int limit = 10,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<OfferClaim> claims = _claims
            .Where(c => c.UserId == userId && (status is null || c.Status == status))
            .OrderByDescending(c => c.CreatedAtUtc)
            .Take(Math.Max(1, limit))
            .ToList();
        return Task.FromResult(claims);
    }

    public Task<int> ExpireActiveByMerchantAsync(
        Guid merchantId,
        DateTime nowUtc,
        CancellationToken cancellationToken = default)
    {
        int changed = _claims.Count(c =>
            c.MerchantId == merchantId &&
            c.Status == OfferClaimStatus.Active &&
            c.Expire(nowUtc).IsSuccess);
        return Task.FromResult(changed);
    }
}
