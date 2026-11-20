using Sensus.Domain.Entities;
using Sensus.Domain.Repositories;

namespace Sensus.TestKit.Fakes;

public sealed class InMemoryOfferLikeRepository : IOfferLikeRepository
{
    private readonly List<OfferLike> _likes = [];

    public Task<bool> ToggleLikeAsync(
        Guid userId,
        Guid offerId,
        DateTime nowUtc,
        CancellationToken cancellationToken = default)
    {
        OfferLike? existing = _likes.FirstOrDefault(l => l.UserId == userId && l.OfferId == offerId);
        if (existing is not null)
        {
            _likes.Remove(existing);
            return Task.FromResult(false);
        }

        var created = OfferLike.Create(userId, offerId, nowUtc);
        if (created.IsFailure)
        {
            return Task.FromResult(false);
        }

        _likes.Add(created.Value);
        return Task.FromResult(true);
    }

    public Task<int> CountByOfferIdAsync(Guid offerId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_likes.Count(l => l.OfferId == offerId));

    public Task<Dictionary<Guid, int>> CountByOfferIdsAsync(
        IEnumerable<Guid> offerIds,
        CancellationToken cancellationToken = default)
    {
        HashSet<Guid> targetIds = offerIds.ToHashSet();
        Dictionary<Guid, int> counts = _likes
            .Where(l => targetIds.Contains(l.OfferId))
            .GroupBy(l => l.OfferId)
            .ToDictionary(g => g.Key, g => g.Count());
        return Task.FromResult(counts);
    }

    public Task<HashSet<Guid>> GetLikedOfferIdsAsync(
        Guid userId,
        IEnumerable<Guid> offerIds,
        CancellationToken cancellationToken = default)
    {
        HashSet<Guid> targetIds = offerIds.ToHashSet();
        HashSet<Guid> liked = _likes
            .Where(l => l.UserId == userId && targetIds.Contains(l.OfferId))
            .Select(l => l.OfferId)
            .ToHashSet();
        return Task.FromResult(liked);
    }

    public Task<IReadOnlyList<OfferLike>> GetByUserIdsAsync(
        IEnumerable<Guid> userIds,
        int limit,
        CancellationToken cancellationToken = default)
    {
        HashSet<Guid> targetUsers = userIds.ToHashSet();
        IReadOnlyList<OfferLike> items = _likes
            .Where(l => targetUsers.Contains(l.UserId))
            .OrderByDescending(l => l.CreatedAtUtc)
            .Take(Math.Max(0, limit))
            .ToList();
        return Task.FromResult(items);
    }
}
