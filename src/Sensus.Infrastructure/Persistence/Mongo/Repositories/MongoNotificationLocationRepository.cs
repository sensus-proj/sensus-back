using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.GeoJsonObjectModel;
using Sensus.Domain.Repositories;
using Sensus.Infrastructure.Persistence.Mongo.Documents;

namespace Sensus.Infrastructure.Persistence.Mongo.Repositories;

public sealed class MongoNotificationLocationRepository(MongoDbContext context) : INotificationLocationRepository
{
    public async Task SaveAsync(Guid userId, double latitude, double longitude, DateTime sharedAtUtc, DateTime expiresAtUtc,
        CancellationToken cancellationToken)
    {
        var document = new NotificationLocationDocument
        {
            UserId = userId,
            Coordinates = GeoJson.Point(GeoJson.Geographic(longitude, latitude)),
            SharedAtUtc = sharedAtUtc,
            ExpiresAtUtc = expiresAtUtc
        };
        FilterDefinition<NotificationLocationDocument> currentOrOlder =
            Builders<NotificationLocationDocument>.Filter.Where(d => d.UserId == userId && d.SharedAtUtc <= sharedAtUtc);
        try
        {
            await context.NotificationLocations.ReplaceOneAsync(currentOrOlder, document,
                new ReplaceOptions { IsUpsert = true }, cancellationToken);
        }
        catch (MongoWriteException exception) when (exception.WriteError.Category == ServerErrorCategory.DuplicateKey)
        {
            await context.NotificationLocations.ReplaceOneAsync(currentOrOlder, document, cancellationToken: cancellationToken);
        }
    }

    public Task DeleteAsync(Guid userId, CancellationToken cancellationToken) =>
        context.NotificationLocations.DeleteOneAsync(d => d.UserId == userId, cancellationToken);

    // Usa el índice 2dsphere de "coordinates" declarado en MongoDbContext; el TTL de expiresAtUtc no es inmediato,
    // por eso la vigencia también se filtra en la consulta.
    public async Task<IReadOnlyList<NotificationLocationMatch>> GetActiveNearAsync(double latitude, double longitude,
        double maxDistanceKm, DateTime nowUtc, int limit, CancellationToken cancellationToken)
    {
        var geoNear = new BsonDocument("$geoNear", new BsonDocument
        {
            { "near", new BsonDocument { { "type", "Point" }, { "coordinates", new BsonArray { longitude, latitude } } } },
            { "key", "coordinates" },
            { "distanceField", "distanceMeters" },
            { "maxDistance", maxDistanceKm * 1000d },
            { "spherical", true },
            { "query", new BsonDocument("expiresAtUtc", new BsonDocument("$gt", nowUtc)) }
        });
        PipelineDefinition<NotificationLocationDocument, BsonDocument> pipeline =
            PipelineDefinition<NotificationLocationDocument, BsonDocument>.Create(
                geoNear,
                new BsonDocument("$limit", limit),
                new BsonDocument("$project", new BsonDocument("distanceMeters", 1)));

        List<BsonDocument> results = await context.NotificationLocations
            .Aggregate(pipeline, cancellationToken: cancellationToken)
            .ToListAsync(cancellationToken);
        return results
            .Select(d => new NotificationLocationMatch(Guid.Parse(d["_id"].AsString), d["distanceMeters"].ToDouble() / 1000d))
            .ToList();
    }
}
