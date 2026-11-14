using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;
using Sensus.Domain.Entities;
using Sensus.Domain.Enums;
using Sensus.Domain.Models;
using Sensus.Domain.Repositories;
using Sensus.Infrastructure.Persistence.Mongo.Documents;

namespace Sensus.Infrastructure.Persistence.Mongo.Repositories;

/// <summary>
/// Implementación de IOfferRepository con persistencia directa en MongoDB Atlas y actualización masiva de expiración (SE-022 a SE-026).
/// </summary>
public sealed class MongoOfferRepository : IOfferRepository
{
    private readonly MongoDbContext _context;
    private readonly ILogger<MongoOfferRepository> _logger;

    public MongoOfferRepository(
        MongoDbContext context,
        ILogger<MongoOfferRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<Offer?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var filter = Builders<OfferDocument>.Filter.Eq(o => o.Id, id);
        var document = await _context.Offers.Find(filter).FirstOrDefaultAsync(cancellationToken);
        return document?.ToDomain();
    }

    public async Task<IReadOnlyList<Offer>> GetByMerchantIdAsync(Guid merchantId, CancellationToken cancellationToken = default)
    {
        var filter = Builders<OfferDocument>.Filter.Eq(o => o.MerchantId, merchantId);
        var sort = Builders<OfferDocument>.Sort.Descending(o => o.CreatedAtUtc);
        var documents = await _context.Offers.Find(filter).Sort(sort).ToListAsync(cancellationToken);
        return documents.Select(d => d.ToDomain()).ToList();
    }

    public async Task AddAsync(Offer offer, CancellationToken cancellationToken = default)
    {
        var document = OfferDocument.FromDomain(offer);
        await _context.Offers.InsertOneAsync(document, cancellationToken: cancellationToken);
    }

    public async Task UpdateAsync(Offer offer, CancellationToken cancellationToken = default)
    {
        var document = OfferDocument.FromDomain(offer);
        var filter = Builders<OfferDocument>.Filter.Eq(o => o.Id, offer.Id);
        await _context.Offers.ReplaceOneAsync(filter, document, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Transición masiva y atómica en MongoDB para expirar ofertas cuya vigencia finalizó.
    /// Reemplaza la necesidad de Triggers o SQL Agent Jobs aprovechando el índice compuesto (Status, EndsAt).
    /// </summary>
    public async Task<int> ExpireOffersAsync(DateTime nowUtc, CancellationToken cancellationToken = default)
    {
        var filter = Builders<OfferDocument>.Filter.And(
            Builders<OfferDocument>.Filter.In(o => o.Status, [OfferStatus.Published.ToString(), OfferStatus.Paused.ToString()]),
            Builders<OfferDocument>.Filter.Lte(o => o.EndsAt, nowUtc)
        );

        var update = Builders<OfferDocument>.Update
            .Set(o => o.Status, OfferStatus.Expired.ToString())
            .Set(o => o.LastModifiedAtUtc, nowUtc);

        var result = await _context.Offers.UpdateManyAsync(filter, update, cancellationToken: cancellationToken);
        _logger.LogInformation("Actualización masiva de vigencia en MongoDB: {ModifiedCount} ofertas expiradas a las {NowUtc}.",
            result.ModifiedCount, nowUtc);

        return (int)result.ModifiedCount;
    }

    /// <summary>
    /// Agregación geoespacial: $geoNear sobre el índice 2dsphere, $lookup a comercios para validar
    /// actividad y categoría, y $facet para devolver conteo y página en una sola consulta
    /// (FR-007-01 a FR-007-07, NFR-007-02).
    /// </summary>
    public async Task<NearbyOffersPage> GetNearbyAsync(
        double latitude,
        double longitude,
        int radiusMeters,
        MerchantCategory? category,
        int pageNumber,
        int pageSize,
        DateTime nowUtc,
        CancellationToken cancellationToken = default)
    {
        string comerciosCollection = _context.Comercios.CollectionNamespace.CollectionName;

        var pipeline = new List<BsonDocument>
        {
            new("$geoNear", new BsonDocument
            {
                { "near", new BsonDocument
                    {
                        { "type", "Point" },
                        { "coordinates", new BsonArray { longitude, latitude } }
                    }
                },
                { "distanceField", "distanceMeters" },
                { "maxDistance", radiusMeters },
                { "spherical", true },
                { "query", BuildVisibilityFilter(nowUtc) }
            }),
            new("$lookup", new BsonDocument
            {
                { "from", comerciosCollection },
                { "localField", "merchantId" },
                { "foreignField", "_id" },
                { "as", "merchant" }
            }),
            new("$unwind", "$merchant"),
            new("$match", new BsonDocument("merchant.activo", true))
        };

        if (category.HasValue)
        {
            pipeline.Add(new BsonDocument("$match", new BsonDocument("merchant.categoria", category.Value.ToString())));
        }

        pipeline.Add(new BsonDocument("$sort", new BsonDocument
        {
            { "distanceMeters", 1 },
            { "createdAtUtc", -1 }
        }));

        pipeline.Add(new BsonDocument("$facet", new BsonDocument
        {
            {
                "metadata",
                new BsonArray { new BsonDocument("$count", "total") }
            },
            {
                "data",
                new BsonArray
                {
                    new BsonDocument("$skip", Math.Max(0, (pageNumber - 1) * pageSize)),
                    new BsonDocument("$limit", pageSize)
                }
            }
        }));

        var results = await _context.Offers
            .Aggregate<BsonDocument>(pipeline, cancellationToken: cancellationToken)
            .ToListAsync(cancellationToken);

        BsonDocument? facet = results.FirstOrDefault();
        if (facet is null)
        {
            return new NearbyOffersPage([], 0);
        }

        int totalCount = facet.TryGetValue("metadata", out var metadata) &&
                         metadata.IsBsonArray &&
                         metadata.AsBsonArray.Count > 0
            ? metadata.AsBsonArray[0]["total"].ToInt32()
            : 0;

        var items = new List<NearbyOfferHit>();
        if (facet.TryGetValue("data", out var data) && data.IsBsonArray)
        {
            foreach (BsonValue entry in data.AsBsonArray)
            {
                if (entry.IsBsonDocument)
                {
                    items.Add(MapNearbyOffer(entry.AsBsonDocument));
                }
            }
        }

        return new NearbyOffersPage(items, totalCount);
    }

    /// <summary>
    /// Reserva atómica de una unidad de stock (FR-033-01, FR-033-02, NFR-033-02).
    /// </summary>
    public async Task<bool> TryReserveStockAsync(
        Guid offerId,
        DateTime nowUtc,
        CancellationToken cancellationToken = default)
    {
        var filter = Builders<OfferDocument>.Filter.And(
            Builders<OfferDocument>.Filter.Eq(o => o.Id, offerId),
            Builders<OfferDocument>.Filter.Eq(o => o.Status, OfferStatus.Published.ToString()),
            Builders<OfferDocument>.Filter.Gt(o => o.Stock, 0),
            Builders<OfferDocument>.Filter.Lte(o => o.StartsAt, nowUtc),
            Builders<OfferDocument>.Filter.Gt(o => o.EndsAt, nowUtc));

        var update = Builders<OfferDocument>.Update.Inc(o => o.Stock, -1);

        UpdateResult result = await _context.Offers.UpdateOneAsync(
            filter,
            update,
            cancellationToken: cancellationToken);

        return result.ModifiedCount == 1;
    }

    /// <summary>
    /// Devuelve unidades al inventario tras expirar o no completar un apartado (FR-033-05).
    /// </summary>
    public async Task<bool> ReturnStockAsync(
        Guid offerId,
        int units,
        CancellationToken cancellationToken = default)
    {
        if (units <= 0)
        {
            return false;
        }

        var filter = Builders<OfferDocument>.Filter.Eq(o => o.Id, offerId);
        var update = Builders<OfferDocument>.Update.Inc(o => o.Stock, units);

        UpdateResult result = await _context.Offers.UpdateOneAsync(
            filter,
            update,
            cancellationToken: cancellationToken);

        return result.ModifiedCount == 1;
    }

    /// <summary>
    /// Incremento atómico del contador de vistas (FR-032-03, CA-032-03).
    /// </summary>
    public async Task<bool> IncrementViewCountAsync(
        Guid offerId,
        CancellationToken cancellationToken = default)
    {
        var filter = Builders<OfferDocument>.Filter.Eq(o => o.Id, offerId);
        var update = Builders<OfferDocument>.Update.Inc(o => o.ViewCount, 1);

        UpdateResult result = await _context.Offers.UpdateOneAsync(
            filter,
            update,
            cancellationToken: cancellationToken);

        return result.ModifiedCount == 1;
    }

    private static BsonDocument BuildVisibilityFilter(DateTime nowUtc) =>
        new()
        {
            { "status", OfferStatus.Published.ToString() },
            { "stock", new BsonDocument("$gt", 0) },
            { "startsAt", new BsonDocument("$lte", nowUtc) },
            { "endsAt", new BsonDocument("$gt", nowUtc) }
        };

    private static NearbyOfferHit MapNearbyOffer(BsonDocument document)
    {
        string statusValue = GetString(document, "status") ?? OfferStatus.Published.ToString();
        OfferStatus status = Enum.TryParse(statusValue, true, out OfferStatus parsedStatus)
            ? parsedStatus
            : OfferStatus.Published;

        string categoryValue = GetString(document, "merchantCategory") ?? MerchantCategory.Otros.ToString();
        MerchantCategory category = MerchantCategoryCatalog.TryParse(categoryValue, out MerchantCategory parsedCategory)
            ? parsedCategory
            : MerchantCategory.Otros;

        Offer offer = Offer.Reconstitute(
            document["_id"].AsGuid,
            document["merchantId"].AsGuid,
            document["title"].AsString,
            document["description"].AsString,
            document["originalPrice"].ToDecimal(),
            document["discountPercentage"].ToInt32(),
            document["discountedPrice"].ToDecimal(),
            document["stock"].ToInt32(),
            document["startsAt"].ToUniversalTime(),
            document["endsAt"].ToUniversalTime(),
            status,
            GetString(document, "imageUrl"),
            GetNullableDouble(document, "latitude"),
            GetNullableDouble(document, "longitude"),
            document["createdAtUtc"].ToUniversalTime(),
            GetNullableDate(document, "lastModifiedAtUtc"),
            GetInt32(document, "viewCount"));

        return new NearbyOfferHit(
            offer,
            document["distanceMeters"].ToDouble(),
            GetString(document, "merchantName") ?? string.Empty,
            category);
    }

    private static string? GetString(BsonDocument document, string field) =>
        document.TryGetValue(field, out BsonValue value) && !value.IsBsonNull ? value.AsString : null;

    private static double? GetNullableDouble(BsonDocument document, string field) =>
        document.TryGetValue(field, out BsonValue value) && !value.IsBsonNull && value.IsNumeric
            ? value.ToDouble()
            : null;

    private static DateTime? GetNullableDate(BsonDocument document, string field) =>
        document.TryGetValue(field, out BsonValue value) && value.IsValidDateTime
            ? value.ToUniversalTime()
            : null;

    private static int GetInt32(BsonDocument document, string field) =>
        document.TryGetValue(field, out BsonValue value) && value.IsNumeric ? value.ToInt32() : 0;
}
