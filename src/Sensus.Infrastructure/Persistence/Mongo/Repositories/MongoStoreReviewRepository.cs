using MongoDB.Bson;
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

    public async Task<(IReadOnlyList<StoreReview> Items, long TotalCount)> GetPagedByMerchantIdAsync(
        Guid merchantId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        int safePage = Math.Max(1, pageNumber);
        int safePageSize = Math.Clamp(pageSize, 1, 100);
        int skip = (safePage - 1) * safePageSize;

        var filter = Builders<StoreReviewDocument>.Filter.Eq(x => x.MerchantId, merchantId);

        long totalCount = await _context.StoreReviews.CountDocumentsAsync(filter, cancellationToken: cancellationToken);

        List<StoreReviewDocument> docs = await _context.StoreReviews
            .Find(filter)
            .SortByDescending(x => x.CreatedAtUtc)
            .Skip(skip)
            .Limit(safePageSize)
            .ToListAsync(cancellationToken);

        return (docs.Select(d => d.ToDomain()).ToList(), totalCount);
    }

    public async Task<StoreReviewRatingStats> GetRatingSummaryAsync(
        Guid merchantId,
        CancellationToken cancellationToken = default)
    {
        var matchFilter = Builders<StoreReviewDocument>.Filter.Eq(x => x.MerchantId, merchantId);

        var pipeline = _context.StoreReviews.Aggregate()
            .Match(matchFilter)
            .Group(new BsonDocument
            {
                { "_id", "$rating" },
                { "count", new BsonDocument("$sum", 1) }
            });

        List<BsonDocument> results = await pipeline.ToListAsync(cancellationToken);

        var countsByStar = new Dictionary<int, int>();
        foreach (BsonDocument doc in results)
        {
            if (doc.TryGetValue("_id", out BsonValue? idVal) && !idVal.IsBsonNull &&
                doc.TryGetValue("count", out BsonValue? countVal) && !countVal.IsBsonNull)
            {
                countsByStar[idVal.ToInt32()] = countVal.ToInt32();
            }
        }

        int total = countsByStar.Values.Sum();
        double avg = total == 0 ? 0.0 : Math.Round(countsByStar.Sum(kvp => (long)kvp.Key * kvp.Value) / (double)total, 1);

        return new StoreReviewRatingStats(avg, total, countsByStar);
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
