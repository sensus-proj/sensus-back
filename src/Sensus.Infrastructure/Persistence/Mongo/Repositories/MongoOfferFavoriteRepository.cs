using MongoDB.Driver;
using Sensus.Domain.Entities;
using Sensus.Domain.Repositories;
using Sensus.Infrastructure.Persistence.Mongo.Documents;

namespace Sensus.Infrastructure.Persistence.Mongo.Repositories;

/// <summary>
/// Implementación MongoDB para favoritos de ofertas (Spec 038).
/// </summary>
public sealed class MongoOfferFavoriteRepository : IOfferFavoriteRepository
{
    private readonly MongoDbContext _context;

    public MongoOfferFavoriteRepository(MongoDbContext context)
    {
        _context = context;
    }

    public async Task<bool> ToggleFavoriteAsync(
        Guid userId,
        Guid offerId,
        DateTime nowUtc,
        CancellationToken cancellationToken = default)
    {
        var filter = Builders<OfferFavoriteDocument>.Filter.And(
            Builders<OfferFavoriteDocument>.Filter.Eq(x => x.UserId, userId),
            Builders<OfferFavoriteDocument>.Filter.Eq(x => x.OfferId, offerId));

        DeleteResult deleted = await _context.OfferFavorites.DeleteOneAsync(filter, cancellationToken);
        if (deleted.DeletedCount > 0)
        {
            return false;
        }

        var entity = OfferFavorite.Create(userId, offerId, nowUtc);
        if (entity.IsFailure)
        {
            return false;
        }

        try
        {
            await _context.OfferFavorites.InsertOneAsync(
                OfferFavoriteDocument.FromDomain(entity.Value),
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
        long count = await _context.OfferFavorites.CountDocumentsAsync(
            x => x.OfferId == offerId,
            cancellationToken: cancellationToken);

        return (int)count;
    }

    public async Task<bool> IsFavoritedAsync(Guid userId, Guid offerId, CancellationToken cancellationToken = default)
    {
        var filter = Builders<OfferFavoriteDocument>.Filter.And(
            Builders<OfferFavoriteDocument>.Filter.Eq(x => x.UserId, userId),
            Builders<OfferFavoriteDocument>.Filter.Eq(x => x.OfferId, offerId));

        return await _context.OfferFavorites.Find(filter).AnyAsync(cancellationToken);
    }

    public async Task<HashSet<Guid>> GetFavoritedOfferIdsAsync(
        Guid userId,
        IEnumerable<Guid> offerIds,
        CancellationToken cancellationToken = default)
    {
        var list = offerIds.ToList();
        if (list.Count == 0) return [];

        var filter = Builders<OfferFavoriteDocument>.Filter.And(
            Builders<OfferFavoriteDocument>.Filter.Eq(x => x.UserId, userId),
            Builders<OfferFavoriteDocument>.Filter.In(x => x.OfferId, list));

        List<OfferFavoriteDocument> docs = await _context.OfferFavorites
            .Find(filter)
            .ToListAsync(cancellationToken);

        return docs.Select(d => d.OfferId).ToHashSet();
    }
}
