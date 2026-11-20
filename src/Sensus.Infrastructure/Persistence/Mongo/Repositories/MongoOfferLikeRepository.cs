using MongoDB.Driver;
using Sensus.Domain.Entities;
using Sensus.Domain.Repositories;
using Sensus.Infrastructure.Persistence.Mongo.Documents;

namespace Sensus.Infrastructure.Persistence.Mongo.Repositories;

public sealed class MongoOfferLikeRepository : IOfferLikeRepository
{
    private readonly MongoDbContext _context;

    public MongoOfferLikeRepository(MongoDbContext context)
    {
        _context = context;
    }

    public async Task<bool> ToggleLikeAsync(
        Guid userId,
        Guid offerId,
        DateTime nowUtc,
        CancellationToken cancellationToken = default)
    {
        var filter = Builders<OfferLikeDocument>.Filter.And(
            Builders<OfferLikeDocument>.Filter.Eq(x => x.UserId, userId),
            Builders<OfferLikeDocument>.Filter.Eq(x => x.OfferId, offerId));

        DeleteResult deleted = await _context.OfferLikes.DeleteOneAsync(filter, cancellationToken);
        if (deleted.DeletedCount > 0)
        {
            return false;
        }

        var entity = OfferLike.Create(userId, offerId, nowUtc);
        if (entity.IsFailure)
        {
            return false;
        }

        try
        {
            await _context.OfferLikes.InsertOneAsync(
                OfferLikeDocument.FromDomain(entity.Value),
                cancellationToken: cancellationToken);
            return true;
        }
        catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            return true;
        }
    }

    public async Task<int> CountByOfferIdAsync(Guid offerId, CancellationToken cancellationToken = default)
    {
        long count = await _context.OfferLikes.CountDocumentsAsync(
            x => x.OfferId == offerId,
            cancellationToken: cancellationToken);

        return (int)count;
    }

    public async Task<Dictionary<Guid, int>> CountByOfferIdsAsync(
        IEnumerable<Guid> offerIds,
        CancellationToken cancellationToken = default)
    {
        List<Guid> ids = offerIds.Where(id => id != Guid.Empty).Distinct().ToList();
        if (ids.Count == 0)
        {
            return [];
        }

        List<OfferLikeDocument> docs = await _context.OfferLikes
            .Find(Builders<OfferLikeDocument>.Filter.In(x => x.OfferId, ids))
            .ToListAsync(cancellationToken);

        return docs
            .GroupBy(d => d.OfferId)
            .ToDictionary(g => g.Key, g => g.Count());
    }

    public async Task<HashSet<Guid>> GetLikedOfferIdsAsync(
        Guid userId,
        IEnumerable<Guid> offerIds,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
        {
            return [];
        }

        List<Guid> ids = offerIds.Where(id => id != Guid.Empty).Distinct().ToList();
        if (ids.Count == 0)
        {
            return [];
        }

        var filter = Builders<OfferLikeDocument>.Filter.And(
            Builders<OfferLikeDocument>.Filter.Eq(x => x.UserId, userId),
            Builders<OfferLikeDocument>.Filter.In(x => x.OfferId, ids));

        List<OfferLikeDocument> docs = await _context.OfferLikes
            .Find(filter)
            .ToListAsync(cancellationToken);

        return docs.Select(d => d.OfferId).ToHashSet();
    }

    public async Task<IReadOnlyList<OfferLike>> GetByUserIdsAsync(
        IEnumerable<Guid> userIds,
        int limit,
        CancellationToken cancellationToken = default)
    {
        List<Guid> ids = userIds.Where(id => id != Guid.Empty).Distinct().ToList();
        if (ids.Count == 0 || limit <= 0)
        {
            return [];
        }

        List<OfferLikeDocument> docs = await _context.OfferLikes
            .Find(Builders<OfferLikeDocument>.Filter.In(x => x.UserId, ids))
            .SortByDescending(x => x.CreatedAtUtc)
            .Limit(limit)
            .ToListAsync(cancellationToken);

        return docs.Select(d => d.ToDomain()).ToList();
    }
}
