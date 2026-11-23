using Sensus.Domain.Common;
using Sensus.Domain.Enums;

namespace Sensus.Application.Common.Interfaces;

/// <summary>
/// Datos de una decisión de moderación que deben quedar auditados (Spec 021).
/// Ref: FR-021-01 - veredicto, puntajes y umbral de cada decisión.
/// </summary>
public sealed record ModerationAuditEntry(
    Guid TargetId,
    ModerationTargetType TargetType,
    ModerationVerdict Verdict,
    string Decision,
    string ContentContext,
    IReadOnlyList<string>? FlaggedCategories = null,
    IReadOnlyDictionary<string, double>? CategoryScores = null,
    string? ThresholdApplied = null,
    string? ActorId = null,
    ModerationActorType? ActorType = null);

/// <summary>
/// Registra evidencia inmutable de moderación (Spec 021).
/// </summary>
public interface IModerationAuditRecorder
{
    /// <summary>
    /// Persiste la evidencia y propaga el fallo: quien publica contenido debe abortar si no hay auditoría
    /// (fail-closed, FR-021-01).
    /// </summary>
    Task<Result> RecordAsync(ModerationAuditEntry entry, CancellationToken cancellationToken = default);

    /// <summary>
    /// Persiste la evidencia de una decisión ya denegada sin alterar la respuesta al cliente; registra el fallo
    /// en el log (FR-021-01).
    /// </summary>
    Task RecordBestEffortAsync(ModerationAuditEntry entry, CancellationToken cancellationToken = default);
}
