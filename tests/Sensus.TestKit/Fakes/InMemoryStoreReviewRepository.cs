using Sensus.Domain.Entities;
using Sensus.Domain.Repositories;

namespace Sensus.TestKit.Fakes;

public sealed class InMemoryStoreReviewRepository : IStoreReviewRepository
{
    private readonly List<StoreReview> _reviews = [];

    public Task<StoreReview?> GetByIdAsync(Guid reviewId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_reviews.FirstOrDefault(r => r.Id == reviewId));

    public Task<IReadOnlyList<StoreReview>> GetByMerchantIdAsync(
        Guid merchantId,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<StoreReview> items = _reviews
            .Where(r => r.MerchantId == merchantId)
            .OrderByDescending(r => r.CreatedAtUtc)
            .ToList();
        return Task.FromResult(items);
    }

    public Task<(IReadOnlyList<StoreReview> Items, long TotalCount)> GetPagedByMerchantIdAsync(
        Guid merchantId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var matching = _reviews
            .Where(r => r.MerchantId == merchantId)
            .OrderByDescending(r => r.CreatedAtUtc)
            .ToList();

        int safePage = Math.Max(1, pageNumber);
        int safePageSize = Math.Max(1, pageSize);
        int skip = (safePage - 1) * safePageSize;

        IReadOnlyList<StoreReview> items = matching
            .Skip(skip)
            .Take(safePageSize)
            .ToList();

        return Task.FromResult((items, (long)matching.Count));
    }

    public Task<StoreReviewRatingStats> GetRatingSummaryAsync(
        Guid merchantId,
        CancellationToken cancellationToken = default)
    {
        var matching = _reviews.Where(r => r.MerchantId == merchantId).ToList();
        int total = matching.Count;
        double avg = total == 0 ? 0.0 : Math.Round(matching.Average(r => r.Rating), 1);

        var countsByStar = matching
            .GroupBy(r => r.Rating)
            .ToDictionary(g => g.Key, g => g.Count());

        return Task.FromResult(new StoreReviewRatingStats(avg, total, countsByStar));
    }

    public Task<Dictionary<Guid, (double AverageRating, int ReviewCount)>> GetRatingSummariesByMerchantIdsAsync(
        IEnumerable<Guid> merchantIds,
        CancellationToken cancellationToken = default)
    {
        HashSet<Guid> targetIds = merchantIds.ToHashSet();
        var summaries = _reviews
            .Where(r => targetIds.Contains(r.MerchantId))
            .GroupBy(r => r.MerchantId)
            .ToDictionary(
                g => g.Key,
                g => (Math.Round(g.Average(r => r.Rating), 1), g.Count()));
        return Task.FromResult(summaries);
    }

    public Task<IReadOnlyList<StoreReview>> GetRecentActivityByUserIdsAsync(
        IEnumerable<Guid> userIds,
        int limit,
        CancellationToken cancellationToken = default)
    {
        HashSet<Guid> targetUsers = userIds.ToHashSet();
        IReadOnlyList<StoreReview> items = _reviews
            .Where(r => targetUsers.Contains(r.AuthorUserId)
                        || r.Replies.Any(reply => targetUsers.Contains(reply.AuthorUserId)))
            .OrderByDescending(r => r.CreatedAtUtc)
            .Take(Math.Max(0, limit))
            .ToList();
        return Task.FromResult(items);
    }

    public Task AddAsync(StoreReview review, CancellationToken cancellationToken = default)
    {
        _reviews.Add(review);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(StoreReview review, CancellationToken cancellationToken = default)
    {
        int index = _reviews.FindIndex(r => r.Id == review.Id);
        if (index >= 0)
        {
            _reviews[index] = review;
        }

        return Task.CompletedTask;
    }
}
