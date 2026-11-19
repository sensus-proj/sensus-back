using MongoDB.Bson.Serialization.Attributes;

namespace Sensus.Infrastructure.Persistence.Mongo.Documents;

public sealed class ClaimPushReceiptDocument
{
    [BsonId]
    public required string Id { get; init; }

    [BsonElement("expiresAtUtc")]
    public DateTime ExpiresAtUtc { get; init; }
}
