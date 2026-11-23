using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using Sensus.Domain.Entities;
using Sensus.Domain.Enums;

namespace Sensus.Infrastructure.Persistence.Mongo.Documents;

/// <summary>
/// Documento de evidencia de moderación en la colección <c>moderation_audit</c> (Spec 021).
/// Solo se inserta: no existe reemplazo ni borrado desde la aplicación (NFR-021-01).
/// </summary>
public sealed class ModerationAuditDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public Guid Id { get; set; }

    [BsonElement("targetId")]
    [BsonRepresentation(BsonType.String)]
    public Guid TargetId { get; set; }

    [BsonElement("targetType")]
    [BsonRepresentation(BsonType.String)]
    public ModerationTargetType TargetType { get; set; }

    [BsonElement("verdict")]
    [BsonRepresentation(BsonType.String)]
    public ModerationVerdict Verdict { get; set; }

    [BsonElement("decision")]
    public string Decision { get; set; } = string.Empty;

    [BsonElement("contentContext")]
    public string ContentContext { get; set; } = string.Empty;

    [BsonElement("flaggedCategories")]
    public List<string> FlaggedCategories { get; set; } = [];

    [BsonElement("categoryScores")]
    public Dictionary<string, double> CategoryScores { get; set; } = [];

    [BsonElement("thresholdApplied")]
    public string? ThresholdApplied { get; set; }

    [BsonElement("actorType")]
    [BsonRepresentation(BsonType.String)]
    public ModerationActorType ActorType { get; set; }

    [BsonElement("actorId")]
    public string ActorId { get; set; } = string.Empty;

    [BsonElement("correlationId")]
    public string CorrelationId { get; set; } = string.Empty;

    [BsonElement("createdAtUtc")]
    public DateTime CreatedAtUtc { get; set; }

    [BsonElement("retainUntilUtc")]
    public DateTime RetainUntilUtc { get; set; }

    /// <summary>Ref: FR-021-01 - proyección completa de la evidencia.</summary>
    public static ModerationAuditDocument FromDomain(ModerationAuditRecord record) =>
        new()
        {
            Id = record.Id,
            TargetId = record.TargetId,
            TargetType = record.TargetType,
            Verdict = record.Verdict,
            Decision = record.Decision,
            ContentContext = record.ContentContext,
            FlaggedCategories = record.FlaggedCategories.ToList(),
            CategoryScores = record.CategoryScores.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal),
            ThresholdApplied = record.ThresholdApplied,
            ActorType = record.ActorType,
            ActorId = record.ActorId,
            CorrelationId = record.CorrelationId,
            CreatedAtUtc = record.CreatedAtUtc,
            RetainUntilUtc = record.RetainUntilUtc
        };

    /// <summary>Ref: FR-021-03 - reconstrucción para la consulta administrativa.</summary>
    public ModerationAuditRecord ToDomain() =>
        ModerationAuditRecord.Rehydrate(
            Id,
            TargetId,
            TargetType,
            Verdict,
            Decision,
            ContentContext,
            FlaggedCategories,
            CategoryScores,
            ThresholdApplied,
            ActorType,
            ActorId,
            CorrelationId,
            DateTime.SpecifyKind(CreatedAtUtc, DateTimeKind.Utc),
            DateTime.SpecifyKind(RetainUntilUtc, DateTimeKind.Utc));
}
