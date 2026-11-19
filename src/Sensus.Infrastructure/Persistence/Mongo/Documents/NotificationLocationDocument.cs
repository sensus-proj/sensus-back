using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver.GeoJsonObjectModel;

namespace Sensus.Infrastructure.Persistence.Mongo.Documents;

public sealed class NotificationLocationDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public Guid UserId { get; init; }

    [BsonElement("coordinates")]
    public required GeoJsonPoint<GeoJson2DGeographicCoordinates> Coordinates { get; init; }

    [BsonElement("sharedAtUtc")]
    public DateTime SharedAtUtc { get; init; }

    [BsonElement("expiresAtUtc")]
    public DateTime ExpiresAtUtc { get; init; }
}
