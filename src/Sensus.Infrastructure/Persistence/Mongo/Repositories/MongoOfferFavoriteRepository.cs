using MongoDB.Driver;
using Sensus.Domain.Entities;
using Sensus.Domain.Enums;
using Sensus.Domain.Models;
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

    public async Task<FavoriteOffersPage> GetPagedByUserIdAsync(
        Guid userId,
        int pageNumber,
        int pageSize,
        DateTime nowUtc,
        CancellationToken cancellationToken = default)
    {
        DateTime normalizedNow = DateTime.SpecifyKind(nowUtc, DateTimeKind.Utc);
        int safePage = Math.Max(1, pageNumber);
        int safeSize = Math.Clamp(pageSize, 1, 100);

        List<OfferFavoriteDocument> favoriteDocs = await _context.OfferFavorites
            .Find(Builders<OfferFavoriteDocument>.Filter.Eq(x => x.UserId, userId))
            .SortByDescending(x => x.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        if (favoriteDocs.Count == 0)
        {
            return new FavoriteOffersPage([], 0);
        }

        List<Guid> favoriteOfferIds = favoriteDocs.Select(f => f.OfferId).Distinct().ToList();
        FilterDefinition<OfferDocument> offerFilter = Builders<OfferDocument>.Filter.And(
            Builders<OfferDocument>.Filter.In(o => o.Id, favoriteOfferIds),
            Builders<OfferDocument>.Filter.Eq(o => o.Status, OfferStatus.Published.ToString()),
            Builders<OfferDocument>.Filter.Gt(o => o.EndsAt, normalizedNow));

        List<OfferDocument> visibleOfferDocs = await _context.Offers
            .Find(offerFilter)
            .ToListAsync(cancellationToken);

        if (visibleOfferDocs.Count == 0)
        {
            return new FavoriteOffersPage([], 0);
        }

        List<Guid> merchantIds = visibleOfferDocs.Select(o => o.MerchantId).Distinct().ToList();
        List<ComercioDocument> merchantDocs = await _context.Comercios
            .Find(Builders<ComercioDocument>.Filter.In(c => c.Id, merchantIds))
            .ToListAsync(cancellationToken);

        Dictionary<Guid, OfferDocument> offersById = visibleOfferDocs.ToDictionary(o => o.Id);
        Dictionary<Guid, ComercioDocument> merchantsById = merchantDocs.ToDictionary(m => m.Id);

        List<FavoriteOfferEntry> visibleEntries = [];
        foreach (OfferFavoriteDocument fav in favoriteDocs)
        {
            if (!offersById.TryGetValue(fav.OfferId, out OfferDocument? offerDoc))
            {
                continue;
            }

            if (merchantsById.TryGetValue(offerDoc.MerchantId, out ComercioDocument? merchantDoc))
            {
                if (!merchantDoc.Activo)
                {
                    continue;
                }

                visibleEntries.Add(new FavoriteOfferEntry(
                    offerDoc.ToDomain(),
                    merchantDoc.NombreComercial,
                    merchantDoc.Categoria,
                    fav.CreatedAtUtc));
            }
            else
            {
                visibleEntries.Add(new FavoriteOfferEntry(
                    offerDoc.ToDomain(),
                    offerDoc.MerchantName ?? string.Empty,
                    MerchantCategoryCatalog.Default,
                    fav.CreatedAtUtc));
            }
        }

        int totalCount = visibleEntries.Count;
        List<FavoriteOfferEntry> pagedItems = visibleEntries
            .Skip((safePage - 1) * safeSize)
            .Take(safeSize)
            .ToList();

        return new FavoriteOffersPage(pagedItems, totalCount);
    }
}
