using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.GeoJsonObjectModel;
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

    public async Task<bool> UpdateWithVersionCheckAsync(
        Offer offer,
        int expectedVersion,
        CancellationToken cancellationToken = default)
    {
        var document = OfferDocument.FromDomain(offer);
        var filter = Builders<OfferDocument>.Filter.And(
            Builders<OfferDocument>.Filter.Eq(o => o.Id, offer.Id),
            Builders<OfferDocument>.Filter.Eq(o => o.Version, expectedVersion));

        var result = await _context.Offers.ReplaceOneAsync(filter, document, cancellationToken: cancellationToken);
        return result.ModifiedCount == 1;
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
            string categoryText = category.Value.ToString();
            var regex = new BsonRegularExpression($"^{Regex.Escape(categoryText)}$", "i");
            pipeline.Add(new BsonDocument("$match", new BsonDocument("$or", new BsonArray
            {
                new BsonDocument("category", regex),
                new BsonDocument("merchant.categoria", categoryText)
            })));
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

    public async Task<bool> TryReserveStockAsync(
        Guid offerId,
        int quantity,
        DateTime nowUtc,
        CancellationToken cancellationToken = default)
    {
        if (quantity <= 0)
        {
            return false;
        }

        if (quantity == 1)
        {
            return await TryReserveStockAsync(offerId, nowUtc, cancellationToken);
        }

        DateTime utcNow = DateTime.SpecifyKind(nowUtc.ToUniversalTime(), DateTimeKind.Utc);
        var filter = Builders<OfferDocument>.Filter.And(
            Builders<OfferDocument>.Filter.Eq(o => o.Id, offerId),
            Builders<OfferDocument>.Filter.Eq(o => o.Status, OfferStatus.Published.ToString()),
            Builders<OfferDocument>.Filter.Lte(o => o.StartsAt, utcNow),
            Builders<OfferDocument>.Filter.Gt(o => o.EndsAt, utcNow),
            Builders<OfferDocument>.Filter.Gte(o => o.Stock, quantity));

        var update = Builders<OfferDocument>.Update.Inc(o => o.Stock, -quantity);

        UpdateResult result = await _context.Offers.UpdateOneAsync(
            filter,
            update,
            cancellationToken: cancellationToken);

        return result.ModifiedCount == 1;
    }

    /// <summary>
    /// Cuenta las ofertas publicadas y vigentes de un comercio usando el índice compuesto
    /// <c>ix_offers_merchant_status_ends_at</c> (Spec 034, FR-034-05).
    /// </summary>
    public async Task<long> CountActiveByMerchantAsync(
        Guid merchantId,
        DateTime nowUtc,
        CancellationToken cancellationToken = default)
    {
        var filter = Builders<OfferDocument>.Filter.And(
            Builders<OfferDocument>.Filter.Eq(o => o.MerchantId, merchantId),
            Builders<OfferDocument>.Filter.Eq(o => o.Status, OfferStatus.Published.ToString()),
            Builders<OfferDocument>.Filter.Gt(o => o.EndsAt, nowUtc));

        return await _context.Offers.CountDocumentsAsync(filter, cancellationToken: cancellationToken);
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

    /// <summary>
    /// Sincroniza las coordenadas de todas las ofertas del comercio cuando este cambia de ubicación.
    /// </summary>
    public async Task UpdateMerchantLocationAsync(
        Guid merchantId,
        double latitude,
        double longitude,
        CancellationToken cancellationToken = default)
    {
        var filter = Builders<OfferDocument>.Filter.Eq(o => o.MerchantId, merchantId);
        var geoPoint = GeoJson.Point(GeoJson.Geographic(longitude, latitude));
        var update = Builders<OfferDocument>.Update
            .Set(o => o.Latitude, latitude)
            .Set(o => o.Longitude, longitude)
            .Set(o => o.Ubicacion, geoPoint);

        await _context.Offers.UpdateManyAsync(filter, update, cancellationToken: cancellationToken);
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

        BsonDocument? merchantDoc = document.TryGetValue("merchant", out BsonValue merchantValue) &&
                                    merchantValue.IsBsonDocument
            ? merchantValue.AsBsonDocument
            : null;

        string merchantName = (merchantDoc is not null ? GetString(merchantDoc, "nombreComercial") : null)
            ?? GetString(document, "merchantName")
            ?? string.Empty;

        string categoryValue = (merchantDoc is not null ? GetString(merchantDoc, "categoria") : null)
            ?? GetString(document, "merchantCategory")
            ?? MerchantCategory.Otros.ToString();
        MerchantCategory category = MerchantCategoryCatalog.TryParse(categoryValue, out MerchantCategory parsedCategory)
            ? parsedCategory
            : MerchantCategory.Otros;

        double? latitude = (merchantDoc is not null ? GetNullableDouble(merchantDoc, "latitud") : null)
            ?? GetNullableDouble(document, "latitude");
        double? longitude = (merchantDoc is not null ? GetNullableDouble(merchantDoc, "longitud") : null)
            ?? GetNullableDouble(document, "longitude");

        Offer offer = Offer.Reconstitute(
            ParseGuid(document, "_id"),
            ParseGuid(document, "merchantId"),
            GetString(document, "title") ?? string.Empty,
            GetString(document, "description") ?? string.Empty,
            GetDecimal(document, "originalPrice"),
            GetInt32(document, "discountPercentage"),
            GetDecimal(document, "discountedPrice"),
            GetInt32(document, "stock"),
            GetRequiredDateUtc(document, "startsAt"),
            GetRequiredDateUtc(document, "endsAt"),
            status,
            GetString(document, "imageUrl"),
            latitude,
            longitude,
            GetRequiredDateUtc(document, "createdAtUtc"),
            GetNullableDate(document, "lastModifiedAtUtc"),
            GetInt32(document, "viewCount"),
            GetInt32(document, "version") == 0 ? 1 : GetInt32(document, "version"),
            GetString(document, "category"));

        return new NearbyOfferHit(
            offer,
            GetNullableDouble(document, "distanceMeters") ?? 0d,
            merchantName,
            category);
    }

    private static Guid ParseGuid(BsonDocument document, string field)
    {
        if (!document.TryGetValue(field, out BsonValue value) || value.IsBsonNull)
        {
            return Guid.Empty;
        }

        if (value.IsString && Guid.TryParse(value.AsString, out Guid parsed))
        {
            return parsed;
        }

        return value.IsBsonBinaryData ? value.AsGuid : Guid.Empty;
    }

    private static string? GetString(BsonDocument document, string field)
    {
        if (!document.TryGetValue(field, out BsonValue value) || value.IsBsonNull)
        {
            return null;
        }

        return value.IsString ? value.AsString : value.ToString();
    }

    private static decimal GetDecimal(BsonDocument document, string field) =>
        document.TryGetValue(field, out BsonValue value) && !value.IsBsonNull && value.IsNumeric
            ? value.ToDecimal()
            : 0m;

    private static DateTime GetRequiredDateUtc(BsonDocument document, string field) =>
        document.TryGetValue(field, out BsonValue value) && value.IsValidDateTime
            ? value.ToUniversalTime()
            : DateTime.UtcNow;

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

    public async Task<IReadOnlyList<Offer>> SearchAsync(
        string? query,
        MerchantCategory? category,
        decimal? maxBudgetMxn,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var filterBuilder = Builders<OfferDocument>.Filter;
        var filters = new List<FilterDefinition<OfferDocument>>
        {
            filterBuilder.Eq(o => o.Status, OfferStatus.Published.ToString()),
            filterBuilder.Gt(o => o.EndsAt, now),
            filterBuilder.Gt(o => o.Stock, 0)
        };

        if (!string.IsNullOrWhiteSpace(query))
        {
            var escaped = Regex.Escape(query.Trim());
            var regex = new BsonRegularExpression(escaped, "i");
            filters.Add(filterBuilder.Or(
                filterBuilder.Regex(o => o.Title, regex),
                filterBuilder.Regex(o => o.Description, regex)));
        }

        if (category.HasValue)
        {
            filters.Add(filterBuilder.Eq(o => o.Category, category.Value.ToString()));
        }

        if (maxBudgetMxn.HasValue && maxBudgetMxn.Value > 0)
        {
            filters.Add(filterBuilder.Lte(o => o.DiscountedPrice, maxBudgetMxn.Value));
        }

        var combinedFilter = filterBuilder.And(filters);
        var sort = Builders<OfferDocument>.Sort.Descending(o => o.CreatedAtUtc);
        var safeLimit = Math.Clamp(limit, 1, 50);

        var documents = await _context.Offers
            .Find(combinedFilter)
            .Sort(sort)
            .Limit(safeLimit)
            .ToListAsync(cancellationToken);

        return documents.Select(d => d.ToDomain()).ToList();
    }

    public async Task<(IReadOnlyList<Offer> Items, int TotalCount)> GetByMerchantIdPagedAsync(
        Guid merchantId,
        OfferStatus? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var filterBuilder = Builders<OfferDocument>.Filter;
        var filter = filterBuilder.Eq(o => o.MerchantId, merchantId);
        if (status.HasValue)
        {
            filter = filterBuilder.And(filter, filterBuilder.Eq(o => o.Status, status.Value.ToString()));
        }

        var safePage = Math.Max(1, page);
        var safePageSize = Math.Clamp(pageSize, 1, 100);

        var totalCount = (int)await _context.Offers.CountDocumentsAsync(filter, cancellationToken: cancellationToken);
        var sort = Builders<OfferDocument>.Sort.Descending(o => o.CreatedAtUtc);

        var documents = await _context.Offers
            .Find(filter)
            .Sort(sort)
            .Skip((safePage - 1) * safePageSize)
            .Limit(safePageSize)
            .ToListAsync(cancellationToken);

        return (documents.Select(d => d.ToDomain()).ToList(), totalCount);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<FeedOfferCandidate>> GetFeedCandidatesAsync(
        double latitude,
        double longitude,
        int radiusMeters,
        Guid excludeMerchantOwnerUserId,
        int maxCandidates,
        DateTime nowUtc,
        CancellationToken cancellationToken = default)
    {
        string comerciosCollection = _context.Comercios.CollectionNamespace.CollectionName;
        int safeMax = Math.Clamp(maxCandidates, 1, 500);

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
            new("$match", new BsonDocument
            {
                { "merchant.activo", true },
                { "merchant.usuarioId", new BsonDocument("$ne", excludeMerchantOwnerUserId.ToString()) }
            }),
            new("$limit", safeMax)
        };

        List<BsonDocument> docs = await _context.Offers
            .Aggregate<BsonDocument>(pipeline, cancellationToken: cancellationToken)
            .ToListAsync(cancellationToken);

        return docs.Select(d => MapFeedCandidate(d, GetNullableDouble(d, "distanceMeters") ?? 0d)).ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<FeedOfferCandidate>> GetFeedCandidatesByIdsAsync(
        IReadOnlyCollection<Guid> offerIds,
        double referenceLatitude,
        double referenceLongitude,
        Guid excludeMerchantOwnerUserId,
        DateTime nowUtc,
        CancellationToken cancellationToken = default)
    {
        if (offerIds.Count == 0)
        {
            return Array.Empty<FeedOfferCandidate>();
        }

        string comerciosCollection = _context.Comercios.CollectionNamespace.CollectionName;
        BsonDocument matchFilter = BuildVisibilityFilter(nowUtc);
        matchFilter["_id"] = new BsonDocument("$in", new BsonArray(offerIds.Select(id => id.ToString())));

        var pipeline = new List<BsonDocument>
        {
            new("$match", matchFilter),
            new("$lookup", new BsonDocument
            {
                { "from", comerciosCollection },
                { "localField", "merchantId" },
                { "foreignField", "_id" },
                { "as", "merchant" }
            }),
            new("$unwind", "$merchant"),
            new("$match", new BsonDocument
            {
                { "merchant.activo", true },
                { "merchant.usuarioId", new BsonDocument("$ne", excludeMerchantOwnerUserId.ToString()) }
            })
        };

        List<BsonDocument> docs = await _context.Offers
            .Aggregate<BsonDocument>(pipeline, cancellationToken: cancellationToken)
            .ToListAsync(cancellationToken);

        var candidates = new List<FeedOfferCandidate>(docs.Count);
        foreach (BsonDocument doc in docs)
        {
            BsonDocument? merchantDoc = doc.TryGetValue("merchant", out BsonValue mv) && mv.IsBsonDocument ? mv.AsBsonDocument : null;
            double? lat = (merchantDoc is not null ? GetNullableDouble(merchantDoc, "latitud") : null) ?? GetNullableDouble(doc, "latitude");
            double? lng = (merchantDoc is not null ? GetNullableDouble(merchantDoc, "longitud") : null) ?? GetNullableDouble(doc, "longitude");
            double distanceMeters = lat.HasValue && lng.HasValue
                ? ComputeHaversineMeters(referenceLatitude, referenceLongitude, lat.Value, lng.Value)
                : 0d;

            candidates.Add(MapFeedCandidate(doc, distanceMeters));
        }

        return candidates;
    }

    private static FeedOfferCandidate MapFeedCandidate(BsonDocument document, double distanceMeters)
    {
        BsonDocument? merchantDoc = document.TryGetValue("merchant", out BsonValue merchantValue) && merchantValue.IsBsonDocument
            ? merchantValue.AsBsonDocument
            : null;

        string merchantName = (merchantDoc is not null ? GetString(merchantDoc, "nombreComercial") : null)
            ?? GetString(document, "merchantName")
            ?? string.Empty;

        string? rawCategory = GetString(document, "category");
        string normalizedCategory = OfferCategoryCatalog.TryNormalizeLegacy(rawCategory, out OfferCategory parsedCategory)
            ? parsedCategory.ToString()
            : OfferInteractionItem.UncategorizedCategory;

        return new FeedOfferCandidate(
            ParseGuid(document, "_id"),
            GetString(document, "title") ?? string.Empty,
            normalizedCategory,
            GetString(document, "imageUrl"),
            GetDecimal(document, "originalPrice"),
            GetDecimal(document, "discountedPrice"),
            GetInt32(document, "stock"),
            distanceMeters,
            GetRequiredDateUtc(document, "createdAtUtc"),
            GetRequiredDateUtc(document, "endsAt"),
            ParseGuid(document, "merchantId"),
            merchantName,
            GetInt32(document, "viewCount"));
    }

    private static double ComputeHaversineMeters(double lat1, double lon1, double lat2, double lon2)
    {
        const double earthRadiusMeters = 6_371_000d;
        double dLat = (lat2 - lat1) * Math.PI / 180d;
        double dLon = (lon2 - lon1) * Math.PI / 180d;
        double a = Math.Pow(Math.Sin(dLat / 2d), 2d) +
            Math.Cos(lat1 * Math.PI / 180d) * Math.Cos(lat2 * Math.PI / 180d) * Math.Pow(Math.Sin(dLon / 2d), 2d);
        return 2d * earthRadiusMeters * Math.Asin(Math.Min(1d, Math.Sqrt(a)));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<OfferSearchCandidate>> SearchOffersHybridAsync(
        string? normalizedQuery,
        string? rawQuery,
        double latitude,
        double longitude,
        int radiusMeters,
        int maxCandidates,
        DateTime nowUtc,
        CancellationToken cancellationToken = default)
    {
        string comerciosCollection = _context.Comercios.CollectionNamespace.CollectionName;
        IMongoCollection<BsonDocument> offersBson = _context.Database.GetCollection<BsonDocument>(
            _context.Offers.CollectionNamespace.CollectionName);
        int safeMax = Math.Clamp(maxCandidates, 1, 500);

        if (string.IsNullOrWhiteSpace(normalizedQuery))
        {
            var fallbackPipeline = new List<BsonDocument>
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
                LookupMerchantStage(comerciosCollection),
                new("$unwind", "$merchant"),
                new("$match", new BsonDocument("merchant.activo", true)),
                new("$sort", new BsonDocument { { "distanceMeters", 1 }, { "viewCount", -1 } }),
                new("$limit", safeMax)
            };

            List<BsonDocument> fallbackDocs = await offersBson
                .Aggregate<BsonDocument>(fallbackPipeline, cancellationToken: cancellationToken)
                .ToListAsync(cancellationToken);

            return fallbackDocs
                .Select(d => MapSearchCandidate(d, latitude, longitude, GetNullableDouble(d, "distanceMeters")))
                .Where(c => c.DistanceMeters <= radiusMeters)
                .ToList();
        }

        double radiusRadians = Math.Max(1, radiusMeters) / 6_371_000d;
        var geoWithinFilter = new BsonDocument("ubicacion", new BsonDocument("$geoWithin", new BsonDocument(
            "$centerSphere",
            new BsonArray { new BsonArray { longitude, latitude }, radiusRadians })));
        BsonDocument visibilityFilter = BuildVisibilityFilter(nowUtc);

        var merged = new Dictionary<Guid, OfferSearchCandidate>();

        void MergeDocuments(IEnumerable<BsonDocument> docs)
        {
            foreach (BsonDocument doc in docs)
            {
                OfferSearchCandidate candidate = MapSearchCandidate(doc, latitude, longitude, GetNullableDouble(doc, "distanceMeters"));
                if (candidate.OfferId == Guid.Empty || candidate.DistanceMeters > radiusMeters)
                {
                    continue;
                }

                if (merged.TryGetValue(candidate.OfferId, out OfferSearchCandidate? existing))
                {
                    if (candidate.MongoTextScore > existing.MongoTextScore)
                    {
                        merged[candidate.OfferId] = candidate;
                    }
                }
                else
                {
                    merged[candidate.OfferId] = candidate;
                }
            }
        }

        // Canal A: índice de texto MongoDB con default_language = "spanish" (stemming español)
        string safeRawQuery = rawQuery ?? normalizedQuery;
        var textMatchFilter = new BsonDocument("$and", new BsonArray
        {
            visibilityFilter,
            geoWithinFilter,
            new BsonDocument("$text", new BsonDocument
            {
                { "$search", safeRawQuery },
                { "$language", "spanish" }
            })
        });

        var textPipeline = new List<BsonDocument>
        {
            new("$match", textMatchFilter),
            new("$addFields", new BsonDocument("textScore", new BsonDocument("$meta", "textScore"))),
            new("$sort", new BsonDocument { { "textScore", -1 }, { "createdAtUtc", -1 } }),
            new("$limit", safeMax),
            LookupMerchantStage(comerciosCollection),
            new("$unwind", "$merchant"),
            new("$match", new BsonDocument("merchant.activo", true))
        };

        List<BsonDocument> textDocs = await offersBson
            .Aggregate<BsonDocument>(textPipeline, cancellationToken: cancellationToken)
            .ToListAsync(cancellationToken);
        MergeDocuments(textDocs);

        // Canal B: índice ascendente ix_offers_title_normalized para coincidencia por prefijo (>= 2 chars)
        string escapedPrefix = Regex.Escape(normalizedQuery);
        var prefixRegex = new BsonRegularExpression($"^{escapedPrefix}");
        var prefixMatchFilter = new BsonDocument("$and", new BsonArray
        {
            visibilityFilter,
            geoWithinFilter,
            new BsonDocument("titleNormalized", prefixRegex)
        });

        var prefixPipeline = new List<BsonDocument>
        {
            new("$match", prefixMatchFilter),
            new("$limit", safeMax),
            LookupMerchantStage(comerciosCollection),
            new("$unwind", "$merchant"),
            new("$match", new BsonDocument("merchant.activo", true))
        };

        List<BsonDocument> prefixDocs = await offersBson
            .Aggregate<BsonDocument>(prefixPipeline, cancellationToken: cancellationToken)
            .ToListAsync(cancellationToken);
        MergeDocuments(prefixDocs);

        // Canal C: coincidencia exacta de categoría del catálogo y comercios activos por nombre
        HashSet<OfferCategory> matchedCategories = OfferTextNormalizer.ExtractMatchingCategories(normalizedQuery);
        if (matchedCategories.Count > 0)
        {
            var categoryTokens = new BsonArray();
            foreach (OfferCategory cat in matchedCategories)
            {
                categoryTokens.Add(cat.ToString());
                categoryTokens.Add(OfferCategoryCatalog.GetSpanishName(cat));
            }

            var categoryPipeline = new List<BsonDocument>
            {
                new("$match", new BsonDocument("$and", new BsonArray
                {
                    visibilityFilter,
                    geoWithinFilter,
                    new BsonDocument("category", new BsonDocument("$in", categoryTokens))
                })),
                new("$limit", safeMax),
                LookupMerchantStage(comerciosCollection),
                new("$unwind", "$merchant"),
                new("$match", new BsonDocument("merchant.activo", true))
            };

            List<BsonDocument> categoryDocs = await offersBson
                .Aggregate<BsonDocument>(categoryPipeline, cancellationToken: cancellationToken)
                .ToListAsync(cancellationToken);
            MergeDocuments(categoryDocs);
        }

        IMongoCollection<BsonDocument> comerciosBson = _context.Database.GetCollection<BsonDocument>(comerciosCollection);
        List<BsonDocument> activeMerchants = await comerciosBson
            .Find(new BsonDocument("$and", new BsonArray
            {
                new BsonDocument("activo", true),
                new BsonDocument("ubicacion", new BsonDocument("$geoWithin", new BsonDocument(
                    "$centerSphere",
                    new BsonArray { new BsonArray { longitude, latitude }, radiusRadians })))
            }))
            .Project(new BsonDocument { { "_id", 1 }, { "nombreComercial", 1 } })
            .Limit(safeMax)
            .ToListAsync(cancellationToken);

        List<string> matchingMerchantIds = activeMerchants
            .Where(m =>
            {
                string merchantNorm = OfferTextNormalizer.Normalize(GetString(m, "nombreComercial"));
                return !string.IsNullOrEmpty(merchantNorm) && merchantNorm.Contains(normalizedQuery, StringComparison.Ordinal);
            })
            .Select(m => GetString(m, "_id"))
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id!)
            .ToList();

        if (matchingMerchantIds.Count > 0)
        {
            var merchantOffersPipeline = new List<BsonDocument>
            {
                new("$match", new BsonDocument("$and", new BsonArray
                {
                    visibilityFilter,
                    geoWithinFilter,
                    new BsonDocument("merchantId", new BsonDocument("$in", new BsonArray(matchingMerchantIds)))
                })),
                new("$limit", safeMax),
                LookupMerchantStage(comerciosCollection),
                new("$unwind", "$merchant"),
                new("$match", new BsonDocument("merchant.activo", true))
            };

            List<BsonDocument> merchantOfferDocs = await offersBson
                .Aggregate<BsonDocument>(merchantOffersPipeline, cancellationToken: cancellationToken)
                .ToListAsync(cancellationToken);
            MergeDocuments(merchantOfferDocs);
        }

        return merged.Values.ToList();
    }

    /// <inheritdoc />
    public async Task<OfferAlternativesLookupResult> GetAlternativesAsync(
        Guid offerId,
        int maxDistanceMeters,
        decimal comparablePriceRatio,
        DateTime nowUtc,
        CancellationToken cancellationToken = default)
    {
        string comerciosCollection = _context.Comercios.CollectionNamespace.CollectionName;
        IMongoCollection<BsonDocument> offersBson = _context.Database.GetCollection<BsonDocument>(
            _context.Offers.CollectionNamespace.CollectionName);

        BsonDocument originMatch = BuildVisibilityFilter(nowUtc);
        originMatch["_id"] = offerId.ToString();

        var originPipeline = new List<BsonDocument>
        {
            new("$match", originMatch),
            LookupMerchantStage(comerciosCollection),
            new("$unwind", "$merchant"),
            new("$match", new BsonDocument("merchant.activo", true)),
            new("$limit", 1)
        };

        BsonDocument? originDoc = await offersBson
            .Aggregate<BsonDocument>(originPipeline, cancellationToken: cancellationToken)
            .FirstOrDefaultAsync(cancellationToken);

        if (originDoc is null)
        {
            return OfferAlternativesLookupResult.NotFoundOrUnavailable();
        }

        BsonDocument? originMerchant = originDoc.TryGetValue("merchant", out BsonValue mv) && mv.IsBsonDocument
            ? mv.AsBsonDocument
            : null;
        double? originLat = GetNullableDouble(originDoc, "latitude") ?? (originMerchant is not null ? GetNullableDouble(originMerchant, "latitud") : null);
        double? originLng = GetNullableDouble(originDoc, "longitude") ?? (originMerchant is not null ? GetNullableDouble(originMerchant, "longitud") : null);

        if (!originLat.HasValue || !originLng.HasValue)
        {
            return OfferAlternativesLookupResult.NotFoundOrUnavailable();
        }

        string? rawCategory = GetString(originDoc, "category");
        if (!OfferCategoryCatalog.TryNormalizeLegacy(rawCategory, out OfferCategory originCategory))
        {
            return OfferAlternativesLookupResult.NotFoundOrUnavailable();
        }

        decimal originFinalPrice = GetDecimal(originDoc, "discountedPrice");
        decimal maxComparablePrice = OfferAlternativesPolicy.ComputeMaxComparableFinalPrice(originFinalPrice, comparablePriceRatio);

        var acceptableCategories = new BsonArray
        {
            originCategory.ToString(),
            OfferCategoryCatalog.GetSpanishName(originCategory),
            OfferCategoryCatalog.RemoveDiacritics(OfferCategoryCatalog.GetSpanishName(originCategory))
        };

        BsonDocument candidateFilter = BuildVisibilityFilter(nowUtc);
        candidateFilter["_id"] = new BsonDocument("$ne", offerId.ToString());
        candidateFilter["category"] = new BsonDocument("$in", acceptableCategories);
        candidateFilter["discountedPrice"] = new BsonDocument("$lte", new BsonDecimal128(maxComparablePrice));

        var alternativesPipeline = new List<BsonDocument>
        {
            new("$geoNear", new BsonDocument
            {
                { "near", new BsonDocument
                    {
                        { "type", "Point" },
                        { "coordinates", new BsonArray { originLng.Value, originLat.Value } }
                    }
                },
                { "distanceField", "distanceMeters" },
                { "maxDistance", maxDistanceMeters },
                { "spherical", true },
                { "query", candidateFilter }
            }),
            LookupMerchantStage(comerciosCollection),
            new("$unwind", "$merchant"),
            new("$match", new BsonDocument("merchant.activo", true)),
            new("$sort", new BsonDocument { { "distanceMeters", 1 }, { "endsAt", -1 } }),
            new("$limit", 20)
        };

        List<BsonDocument> docs = await offersBson
            .Aggregate<BsonDocument>(alternativesPipeline, cancellationToken: cancellationToken)
            .ToListAsync(cancellationToken);

        List<OfferAlternativeCandidate> candidates = docs.Select(doc =>
        {
            BsonDocument? merchantDoc = doc.TryGetValue("merchant", out BsonValue mVal) && mVal.IsBsonDocument
                ? mVal.AsBsonDocument
                : null;
            string merchantName = (merchantDoc is not null ? GetString(merchantDoc, "nombreComercial") : null)
                ?? GetString(doc, "merchantName")
                ?? string.Empty;
            string? docCategory = GetString(doc, "category");
            string canonicalCategory = OfferCategoryCatalog.TryNormalizeLegacy(docCategory, out OfferCategory parsedCat)
                ? parsedCat.ToString()
                : docCategory ?? originCategory.ToString();

            return new OfferAlternativeCandidate(
                ParseGuid(doc, "_id"),
                GetString(doc, "title") ?? string.Empty,
                canonicalCategory,
                GetString(doc, "imageUrl"),
                GetDecimal(doc, "originalPrice"),
                GetDecimal(doc, "discountedPrice"),
                GetInt32(doc, "stock"),
                GetNullableDouble(doc, "distanceMeters") ?? 0d,
                GetRequiredDateUtc(doc, "endsAt"),
                ParseGuid(doc, "merchantId"),
                merchantName);
        }).ToList();

        return new OfferAlternativesLookupResult(true, candidates);
    }

    public async Task<IReadOnlyList<Offer>> GetPendingModerationDueAsync(
        DateTime nowUtc,
        int batchSize,
        CancellationToken cancellationToken = default)
    {
        DateTime normalizedNow = DateTime.SpecifyKind(nowUtc, DateTimeKind.Utc);
        int safeBatch = Math.Clamp(batchSize, 1, 100);
        string pendingStatus = OfferStatus.PendingModeration.ToString();

        var filterBuilder = Builders<OfferDocument>.Filter;
        FilterDefinition<OfferDocument> filter = filterBuilder.And(
            filterBuilder.Eq(o => o.Status, pendingStatus),
            filterBuilder.Or(
                filterBuilder.Eq(o => o.NextModerationRetryAtUtc, null),
                filterBuilder.Lte(o => o.NextModerationRetryAtUtc, normalizedNow)));

        List<OfferDocument> docs = await _context.Offers
            .Find(filter, new FindOptions { Hint = "ix_offers_pending_moderation_retry" })
            .SortBy(o => o.NextModerationRetryAtUtc)
            .Limit(safeBatch)
            .ToListAsync(cancellationToken);

        return docs.Select(d => d.ToDomain()).ToList();
    }

    private static BsonDocument LookupMerchantStage(string comerciosCollection) =>
        new("$lookup", new BsonDocument
        {
            { "from", comerciosCollection },
            { "localField", "merchantId" },
            { "foreignField", "_id" },
            { "as", "merchant" }
        });

    private static OfferSearchCandidate MapSearchCandidate(
        BsonDocument document,
        double referenceLatitude,
        double referenceLongitude,
        double? precomputedDistanceMeters)
    {
        BsonDocument? merchantDoc = document.TryGetValue("merchant", out BsonValue merchantValue) && merchantValue.IsBsonDocument
            ? merchantValue.AsBsonDocument
            : null;

        string merchantName = (merchantDoc is not null ? GetString(merchantDoc, "nombreComercial") : null)
            ?? GetString(document, "merchantName")
            ?? string.Empty;

        double distanceMeters = precomputedDistanceMeters ?? 0d;
        if (!precomputedDistanceMeters.HasValue)
        {
            double? lat = GetNullableDouble(document, "latitude")
                ?? (merchantDoc is not null ? GetNullableDouble(merchantDoc, "latitud") : null);
            double? lng = GetNullableDouble(document, "longitude")
                ?? (merchantDoc is not null ? GetNullableDouble(merchantDoc, "longitud") : null);
            distanceMeters = lat.HasValue && lng.HasValue
                ? ComputeHaversineMeters(referenceLatitude, referenceLongitude, lat.Value, lng.Value)
                : 0d;
        }

        string? rawCategory = GetString(document, "category");
        string normalizedCategory = OfferCategoryCatalog.TryNormalizeLegacy(rawCategory, out OfferCategory parsedCategory)
            ? parsedCategory.ToString()
            : rawCategory ?? OfferInteractionItem.UncategorizedCategory;

        return new OfferSearchCandidate(
            ParseGuid(document, "_id"),
            GetString(document, "title") ?? string.Empty,
            GetString(document, "description") ?? string.Empty,
            normalizedCategory,
            GetString(document, "imageUrl"),
            GetDecimal(document, "originalPrice"),
            GetDecimal(document, "discountedPrice"),
            GetInt32(document, "stock"),
            distanceMeters,
            GetRequiredDateUtc(document, "endsAt"),
            ParseGuid(document, "merchantId"),
            merchantName,
            GetInt32(document, "viewCount"),
            GetNullableDouble(document, "textScore") ?? 0d);
    }
}
