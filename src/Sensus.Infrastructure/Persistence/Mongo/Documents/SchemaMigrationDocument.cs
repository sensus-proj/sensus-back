using MongoDB.Bson.Serialization.Attributes;

namespace Sensus.Infrastructure.Persistence.Mongo.Documents;

/// <summary>
/// Registro de migraciones únicas e idempotentes ejecutadas sobre MongoDB (p. ej. FR-034-09).
/// </summary>
public sealed class SchemaMigrationDocument
{
    [BsonId]
    public string Id { get; set; } = string.Empty;

    [BsonElement("executedAtUtc")]
    public DateTime ExecutedAtUtc { get; set; }

    [BsonElement("totalScanned")]
    public int TotalScanned { get; set; }

    [BsonElement("convertedCount")]
    public int ConvertedCount { get; set; }

    [BsonElement("alreadyCanonicalCount")]
    public int AlreadyCanonicalCount { get; set; }

    [BsonElement("nulledCount")]
    public int NulledCount { get; set; }

    [BsonElement("alreadyNullCount")]
    public int AlreadyNullCount { get; set; }
}
