using Sensus.Domain.Entities;

namespace Sensus.Application.Moderation.Audit;

/// <summary>
/// Proyección de una evidencia de moderación para consumo administrativo (Spec 021).
/// No expone el texto del usuario ni datos personales (FR-021-03, Principio IV).
/// </summary>
public sealed record ModerationAuditRecordDto(
    Guid Id,
    Guid TargetId,
    string TargetType,
    string Verdict,
    string Decision,
    string ContentContext,
    IReadOnlyList<string> FlaggedCategories,
    IReadOnlyDictionary<string, double> CategoryScores,
    string? ThresholdApplied,
    string ActorType,
    string ActorId,
    string CorrelationId,
    DateTime CreatedAtUtc,
    DateTime RetainUntilUtc);

/// <summary>Mapeo dominio a DTO de la auditoría de moderación (Spec 021).</summary>
public static class ModerationAuditMapping
{
    /// <summary>Ref: FR-021-03 - la consulta administrativa expone todos los campos auditados.</summary>
    public static ModerationAuditRecordDto ToDto(this ModerationAuditRecord record) =>
        new(
            record.Id,
            record.TargetId,
            record.TargetType.ToString(),
            record.Verdict.ToString(),
            record.Decision,
            record.ContentContext,
            record.FlaggedCategories,
            record.CategoryScores,
            record.ThresholdApplied,
            record.ActorType.ToString(),
            record.ActorId,
            record.CorrelationId,
            record.CreatedAtUtc,
            record.RetainUntilUtc);
}
