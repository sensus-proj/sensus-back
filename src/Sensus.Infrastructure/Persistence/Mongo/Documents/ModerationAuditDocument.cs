using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using Sensus.Domain.Entities;

namespace Sensus.Infrastructure.Persistence.Mongo.Documents;

/// <summary>
/// Copia anonimizada de evidencia de moderación (Spec 061). Solo metadatos técnicos, sin PII.
/// El campo <c>expiresAtUtc</c> tiene un índice TTL de 90 días configurable.
/// </summary>
public sealed class ModerationAuditDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public Guid Id { get; set; }

    [BsonElement("subjectKey")]
    public string SubjectKey { get; set; } = string.Empty;

    [BsonElement("sourceSanctionId")]
    [BsonRepresentation(BsonType.String)]
    public Guid SourceSanctionId { get; set; }

    [BsonElement("decision")]
    public string Decision { get; set; } = string.Empty;

    [BsonElement("categories")]
    public List<string> Categories { get; set; } = [];

    [BsonElement("scores")]
    public Dictionary<string, double> Scores { get; set; } = new();

    [BsonElement("thresholdApplied")]
    public string? ThresholdApplied { get; set; }

    [BsonElement("action")]
    public string Action { get; set; } = string.Empty;

    [BsonElement("recordedAtUtc")]
    public DateTime RecordedAtUtc { get; set; }

    [BsonElement("expiresAtUtc")]
    public DateTime ExpiresAtUtc { get; set; }

    public static ModerationAuditDocument FromDomain(ModerationAudit entry) =>
        new()
        {
            Id = entry.Id,
            SubjectKey = entry.SubjectKey,
            SourceSanctionId = entry.SourceSanctionId,
            Decision = entry.Decision,
            Categories = entry.Categories.ToList(),
            Scores = new Dictionary<string, double>(entry.Scores),
            ThresholdApplied = entry.ThresholdApplied,
            Action = entry.Action,
            RecordedAtUtc = entry.RecordedAtUtc,
            ExpiresAtUtc = entry.ExpiresAtUtc
        };
}
