using MongoDB.Driver;
using Sensus.Domain.Entities;
using Sensus.Domain.Repositories;
using Sensus.Infrastructure.Persistence.Mongo.Documents;

namespace Sensus.Infrastructure.Persistence.Mongo.Repositories;

public sealed class MongoStoreReviewRepository : IStoreReviewRepository
{
    private readonly MongoDbContext _context;

    public MongoStoreReviewRepository(MongoDbContext context)
    {
        _context = context;
    }

    public async Task<StoreReview?> GetByIdAsync(Guid reviewId, CancellationToken cancellationToken = default)
    {
        StoreReviewDocument? doc = await _context.StoreReviews
            .Find(x => x.Id == reviewId)
            .FirstOrDefaultAsync(cancellationToken);

        return doc?.ToDomain();
    }

    public async Task<IReadOnlyList<StoreReview>> GetByMerchantIdAsync(
        Guid merchantId,
        CancellationToken cancellationToken = default)
    {
        List<StoreReviewDocument> docs = await _context.StoreReviews
            .Find(x => x.MerchantId == merchantId)
            .SortByDescending(x => x.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        return docs.Select(d => d.ToDomain()).ToList();
    }

    public async Task<Dictionary<Guid, (double AverageRating, int ReviewCount)>> GetRatingSummariesByMerchantIdsAsync(
        IEnumerable<Guid> merchantIds,
        CancellationToken cancellationToken = default)
    {
        List<Guid> ids = merchantIds.Where(id => id != Guid.Empty).Distinct().ToList();
        if (ids.Count == 0)
        {
            return [];
        }

        List<StoreReviewDocument> docs = await _context.StoreReviews
            .Find(Builders<StoreReviewDocument>.Filter.In(x => x.MerchantId, ids))
            .ToListAsync(cancellationToken);

        return docs
            .GroupBy(d => d.MerchantId)
            .ToDictionary(
                g => g.Key,
                g => (Math.Round(g.Average(r => r.Rating), 1), g.Count()));
    }

    public async Task<IReadOnlyList<StoreReview>> GetRecentActivityByUserIdsAsync(
        IEnumerable<Guid> userIds,
        int limit,
        CancellationToken cancellationToken = default)
    {
        List<Guid> ids = userIds.Where(id => id != Guid.Empty).Distinct().ToList();
        if (ids.Count == 0 || limit <= 0)
        {
            return [];
        }

        var filter = Builders<StoreReviewDocument>.Filter.Or(
            Builders<StoreReviewDocument>.Filter.In(x => x.AuthorUserId, ids),
            Builders<StoreReviewDocument>.Filter.ElemMatch(
                x => x.Replies,
                r => ids.Contains(r.AuthorUserId)));

        List<StoreReviewDocument> docs = await _context.StoreReviews
            .Find(filter)
            .SortByDescending(x => x.CreatedAtUtc)
            .Limit(limit)
            .ToListAsync(cancellationToken);

        return docs.Select(d => d.ToDomain()).ToList();
    }

    public async Task AddAsync(StoreReview review, CancellationToken cancellationToken = default)
    {
        await _context.StoreReviews.InsertOneAsync(
            StoreReviewDocument.FromDomain(review),
            cancellationToken: cancellationToken);
    }

    public async Task UpdateAsync(StoreReview review, CancellationToken cancellationToken = default)
    {
        await _context.StoreReviews.ReplaceOneAsync(
            x => x.Id == review.Id,
            StoreReviewDocument.FromDomain(review),
            cancellationToken: cancellationToken);
    }
}
