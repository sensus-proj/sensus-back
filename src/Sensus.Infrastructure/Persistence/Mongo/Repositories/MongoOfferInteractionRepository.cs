using MongoDB.Bson;
using MongoDB.Driver;
using Sensus.Domain.Entities;
using Sensus.Domain.Enums;
using Sensus.Domain.Repositories;
using Sensus.Infrastructure.Persistence.Mongo.Documents;

namespace Sensus.Infrastructure.Persistence.Mongo.Repositories;

/// <summary>
/// Repositorio MongoDB sobre <c>offer_interactions</c> con purga multi-versión, agregación indexada diaria
/// y lectura de tamaño en Atlas M0 (Spec 055, FR-055-06, FR-055-07, FR-055-10, NFR-055-04).
/// </summary>
public sealed class MongoOfferInteractionRepository : IOfferInteractionRepository
{
    private readonly MongoDbContext _context;

    public MongoOfferInteractionRepository(MongoDbContext context)
    {
        _context = context;
    }

    public async Task InsertAsync(OfferInteraction interaction, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(interaction);
        OfferInteractionDocument doc = OfferInteractionDocument.FromDomain(interaction);
        await _context.OfferInteractions.InsertOneAsync(doc, cancellationToken: cancellationToken);
    }

    public async Task<long> DeleteByPseudonymsAsync(
        IReadOnlyCollection<(string PseudoUserId, int KeyVersion)> pseudonyms,
        CancellationToken cancellationToken = default)
    {
        if (pseudonyms.Count == 0)
        {
            return 0;
        }

        List<FilterDefinition<OfferInteractionDocument>> pairFilters = pseudonyms
            .Select(p => Builders<OfferInteractionDocument>.Filter.And(
                Builders<OfferInteractionDocument>.Filter.Eq(d => d.PseudoUserId, p.PseudoUserId),
                Builders<OfferInteractionDocument>.Filter.Eq(d => d.KeyVersion, p.KeyVersion)))
            .ToList();

        FilterDefinition<OfferInteractionDocument> filter = pairFilters.Count == 1
            ? pairFilters[0]
            : Builders<OfferInteractionDocument>.Filter.Or(pairFilters);

        DeleteResult result = await _context.OfferInteractions.DeleteManyAsync(filter, cancellationToken);
        return result.DeletedCount;
    }

    public async Task<IReadOnlyList<InteractionQualityAggregate>> AggregateDailyQualityAsync(
        DateTime sinceUtc,
        CancellationToken cancellationToken = default)
    {
        var matchStage = new BsonDocument(
            "$match",
            new BsonDocument("occurredAtUtc", new BsonDocument("$gte", sinceUtc.ToUniversalTime())));

        var unwindStage = new BsonDocument("$unwind", "$items");

        var groupStage = new BsonDocument(
            "$group",
            new BsonDocument
            {
                ["_id"] = new BsonDocument
                {
                    ["rankingVersion"] = "$rankingVersion",
                    ["slot"] = "$items.slot",
                    ["type"] = "$type"
                },
                ["count"] = new BsonDocument("$sum", 1L)
            });

        var pipeline = PipelineDefinition<OfferInteractionDocument, BsonDocument>.Create([matchStage, unwindStage, groupStage]);
        List<BsonDocument> rawRows = await _context.OfferInteractions
            .Aggregate(pipeline, cancellationToken: cancellationToken)
            .ToListAsync(cancellationToken);

        var accumulators = new Dictionary<(string RankingVersion, FeedSlot Slot), (long Impressions, long Opens, long Claims)>();

        foreach (BsonDocument row in rawRows)
        {
            BsonDocument key = row["_id"].AsBsonDocument;
            string rankingVersion = key.GetValue("rankingVersion", "direct").AsString;
            FeedSlot slot = OfferInteractionCatalog.ParseSlot(key.GetValue("slot", "direct").AsString);
            string type = key.GetValue("type", string.Empty).AsString;
            long count = row.GetValue("count", 0L).ToInt64();

            accumulators.TryGetValue((rankingVersion, slot), out (long Impressions, long Opens, long Claims) current);
            current = type switch
            {
                "impression" => (current.Impressions + count, current.Opens, current.Claims),
                "open" => (current.Impressions, current.Opens + count, current.Claims),
                "claim" => (current.Impressions, current.Opens, current.Claims + count),
                _ => current
            };
            accumulators[(rankingVersion, slot)] = current;
        }

        return accumulators
            .OrderBy(kv => kv.Key.RankingVersion, StringComparer.Ordinal)
            .ThenBy(kv => kv.Key.Slot)
            .Select(kv => new InteractionQualityAggregate(
                kv.Key.RankingVersion,
                kv.Key.Slot,
                kv.Value.Impressions,
                kv.Value.Opens,
                kv.Value.Claims))
            .ToList();
    }

    public async Task<InteractionCollectionStorageSize> GetStorageSizeAsync(CancellationToken cancellationToken = default)
    {
        long collectionBytes = 0;
        try
        {
            var collStatsCmd = new BsonDocument("collStats", _context.OfferInteractions.CollectionNamespace.CollectionName);
            BsonDocument collStats = await _context.Database.RunCommandAsync<BsonDocument>(collStatsCmd, cancellationToken: cancellationToken);
            collectionBytes = ReadLongStat(collStats, "storageSize") + ReadLongStat(collStats, "totalIndexSize");
            if (collectionBytes == 0)
            {
                collectionBytes = ReadLongStat(collStats, "size");
            }
        }
        catch (MongoCommandException)
        {
            // Colección aún inexistente antes del primer evento: su tamaño efectivo es 0 bytes.
            collectionBytes = 0;
        }

        var dbStatsCmd = new BsonDocument("dbStats", 1);
        BsonDocument dbStats = await _context.Database.RunCommandAsync<BsonDocument>(dbStatsCmd, cancellationToken: cancellationToken);
        long clusterDataBytes = ReadLongStat(dbStats, "storageSize") + ReadLongStat(dbStats, "indexSize");
        if (clusterDataBytes == 0)
        {
            clusterDataBytes = ReadLongStat(dbStats, "dataSize");
        }

        return new InteractionCollectionStorageSize(collectionBytes, clusterDataBytes);
    }

    private static long ReadLongStat(BsonDocument doc, string field)
    {
        if (!doc.TryGetValue(field, out BsonValue value) || value.IsBsonNull)
        {
            return 0;
        }

        return value.BsonType switch
        {
            BsonType.Int64 => value.AsInt64,
            BsonType.Int32 => value.AsInt32,
            BsonType.Double => (long)value.AsDouble,
            BsonType.Decimal128 => (long)value.AsDecimal128,
            _ => 0
        };
    }
}
