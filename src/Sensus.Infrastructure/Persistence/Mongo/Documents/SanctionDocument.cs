using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using Sensus.Domain.Entities;
using Sensus.Domain.Enums;
using Sensus.Domain.ValueObjects;

namespace Sensus.Infrastructure.Persistence.Mongo.Documents;

/// <summary>
/// Evidencia embebida de una sanción: solo metadatos técnicos de la decisión, sin contenido ni PII.
/// </summary>
public sealed class SanctionEvidenceDocument
{
    [BsonElement("mediaId")]
    [BsonRepresentation(BsonType.String)]
    public Guid? MediaId { get; set; }

    [BsonElement("decision")]
    public string Decision { get; set; } = string.Empty;

    [BsonElement("categories")]
    public List<string> Categories { get; set; } = [];

    [BsonElement("scores")]
    public Dictionary<string, double> Scores { get; set; } = [];

    [BsonElement("thresholdApplied")]
    public string? ThresholdApplied { get; set; }

    [BsonElement("action")]
    public string Action { get; set; } = string.Empty;
}

/// <summary>
/// Documento de sanción (colección <c>sanctions</c>). Registro de auditoría de solo adición.
/// </summary>
public sealed class SanctionDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public Guid Id { get; set; }

    [BsonElement("userId")]
    [BsonRepresentation(BsonType.String)]
    public Guid UserId { get; set; }

    [BsonElement("type")]
    [BsonRepresentation(BsonType.String)]
    public SanctionType Type { get; set; }

    [BsonElement("reason")]
    public string Reason { get; set; } = string.Empty;

    [BsonElement("evidence")]
    public SanctionEvidenceDocument Evidence { get; set; } = new();

    [BsonElement("appliedBy")]
    public string AppliedBy { get; set; } = string.Empty;

    [BsonElement("startsAtUtc")]
    public DateTime StartsAtUtc { get; set; }

    [BsonElement("endsAtUtc")]
    public DateTime? EndsAtUtc { get; set; }

    [BsonElement("revertedBy")]
    [BsonRepresentation(BsonType.String)]
    public Guid? RevertedBy { get; set; }

    [BsonElement("revertedAtUtc")]
    public DateTime? RevertedAtUtc { get; set; }

    [BsonElement("revertReason")]
    public string? RevertReason { get; set; }

    public static SanctionDocument FromDomain(Sanction sanction) =>
        new()
        {
            Id = sanction.Id,
            UserId = sanction.UserId,
            Type = sanction.Type,
            Reason = sanction.Reason,
            Evidence = new SanctionEvidenceDocument
            {
                MediaId = sanction.Evidence.MediaId,
                Decision = sanction.Evidence.Decision,
                Categories = sanction.Evidence.Categories.ToList(),
                Scores = sanction.Evidence.Scores.ToDictionary(pair => pair.Key, pair => pair.Value),
                ThresholdApplied = sanction.Evidence.ThresholdApplied,
                Action = sanction.Evidence.Action
            },
            AppliedBy = sanction.AppliedBy,
            StartsAtUtc = sanction.StartsAtUtc,
            EndsAtUtc = sanction.EndsAtUtc,
            RevertedBy = sanction.RevertedBy,
            RevertedAtUtc = sanction.RevertedAtUtc,
            RevertReason = sanction.RevertReason
        };

    public Sanction ToDomain() =>
        Sanction.Rehydrate(
            Id,
            UserId,
            Type,
            Reason,
            new SanctionEvidence(
                Evidence.MediaId,
                Evidence.Decision,
                Evidence.Categories,
                Evidence.Scores,
                Evidence.ThresholdApplied,
                Evidence.Action),
            AppliedBy,
            DateTime.SpecifyKind(StartsAtUtc, DateTimeKind.Utc),
            EndsAtUtc is { } end ? DateTime.SpecifyKind(end, DateTimeKind.Utc) : null,
            RevertedBy,
            RevertedAtUtc is { } reverted ? DateTime.SpecifyKind(reverted, DateTimeKind.Utc) : null,
            RevertReason);
}
