using Sensus.Domain.Common;
using Sensus.Domain.Enums;

namespace Sensus.Domain.Entities;

/// <summary>
/// Evidencia inmutable de una decisión de moderación (Spec 021).
/// Es un registro de solo adición: no expone operaciones de actualización ni de borrado (NFR-021-01).
/// La retención se materializa con <see cref="RetainUntilUtc"/> y una política TTL en la persistencia (FR-021-04).
/// </summary>
public sealed class ModerationAuditRecord : BaseEntity, IAggregateRoot
{
    /// <summary>Máximo de categorías marcadas que se conservan por decisión.</summary>
    public const int MaxFlaggedCategories = 20;

    /// <summary>Longitud máxima del contexto funcional auditado.</summary>
    public const int MaxContentContextLength = 120;

    /// <summary>Longitud máxima de la decisión textual del motor de moderación.</summary>
    public const int MaxDecisionLength = 32;

    /// <summary>Longitud máxima del resumen de umbral aplicado.</summary>
    public const int MaxThresholdLength = 200;

    /// <summary>Longitud máxima del identificador del actor.</summary>
    public const int MaxActorIdLength = 128;

    /// <summary>Longitud máxima del identificador de correlación de la petición.</summary>
    public const int MaxCorrelationIdLength = 64;

    /// <summary>Retención por defecto para evidencia legal/fiscal (2555 días ≈ 7 años).</summary>
    public const int DefaultRetentionDays = 2555;

    /// <summary>Retención mínima aceptada, en días.</summary>
    public const int MinRetentionDays = 1;

    private static readonly IReadOnlyDictionary<string, double> EmptyScores =
        new Dictionary<string, double>(StringComparer.Ordinal);

    private ModerationAuditRecord(
        Guid targetId,
        ModerationTargetType targetType,
        ModerationVerdict verdict,
        string decision,
        string contentContext,
        IReadOnlyList<string> flaggedCategories,
        IReadOnlyDictionary<string, double> categoryScores,
        string? thresholdApplied,
        ModerationActorType actorType,
        string actorId,
        string correlationId,
        DateTime createdAtUtc,
        DateTime retainUntilUtc)
    {
        Id = Guid.NewGuid();
        TargetId = targetId;
        TargetType = targetType;
        Verdict = verdict;
        Decision = decision;
        ContentContext = contentContext;
        FlaggedCategories = flaggedCategories;
        CategoryScores = categoryScores;
        ThresholdApplied = thresholdApplied;
        ActorType = actorType;
        ActorId = actorId;
        CorrelationId = correlationId;
        CreatedAtUtc = createdAtUtc;
        RetainUntilUtc = retainUntilUtc;
    }

    /// <summary>Identificador del contenido evaluado (activo, comentario u oferta).</summary>
    public Guid TargetId { get; private set; }

    /// <summary>Tipo de contenido evaluado.</summary>
    public ModerationTargetType TargetType { get; private set; }

    /// <summary>Veredicto final aplicado por la plataforma.</summary>
    public ModerationVerdict Verdict { get; private set; }

    /// <summary>Decisión textual del motor de moderación (Approved, Quarantine, Rejected).</summary>
    public string Decision { get; private set; } = string.Empty;

    /// <summary>Contexto funcional de la evaluación; nunca contiene el texto del usuario.</summary>
    public string ContentContext { get; private set; } = string.Empty;

    /// <summary>Categorías marcadas por el motor de moderación.</summary>
    public IReadOnlyList<string> FlaggedCategories { get; private set; } = [];

    /// <summary>Puntajes por categoría devueltos por el motor de moderación.</summary>
    public IReadOnlyDictionary<string, double> CategoryScores { get; private set; } = EmptyScores;

    /// <summary>Resumen del umbral aplicado, cuando el motor lo informa.</summary>
    public string? ThresholdApplied { get; private set; }

    /// <summary>Origen del actor que disparó la decisión.</summary>
    public ModerationActorType ActorType { get; private set; }

    /// <summary>Identificador del actor; <c>system</c> cuando la decisión es automática.</summary>
    public string ActorId { get; private set; } = string.Empty;

    /// <summary>Identificador de correlación de la petición que originó la decisión.</summary>
    public string CorrelationId { get; private set; } = string.Empty;

    /// <summary>Instante UTC de la decisión.</summary>
    public DateTime CreatedAtUtc { get; private set; }

    /// <summary>Instante UTC a partir del cual la evidencia puede purgarse (FR-021-04).</summary>
    public DateTime RetainUntilUtc { get; private set; }

    /// <summary>
    /// Crea la evidencia de una decisión de moderación validando los campos obligatorios.
    /// Ref: FR-021-01 - veredicto, puntajes y umbral; FR-021-04 - retención definida.
    /// </summary>
    public static Result<ModerationAuditRecord> Create(
        Guid targetId,
        ModerationTargetType targetType,
        ModerationVerdict verdict,
        string decision,
        string contentContext,
        IReadOnlyList<string>? flaggedCategories,
        IReadOnlyDictionary<string, double>? categoryScores,
        string? thresholdApplied,
        ModerationActorType actorType,
        string actorId,
        string? correlationId,
        DateTime createdAtUtc,
        int retentionDays)
    {
        if (targetId == Guid.Empty)
        {
            return Result.Failure<ModerationAuditRecord>(
                Error.Validation("ModerationAudit.TargetRequired", "El contenido auditado es obligatorio."));
        }

        if (string.IsNullOrWhiteSpace(decision))
        {
            return Result.Failure<ModerationAuditRecord>(
                Error.Validation("ModerationAudit.DecisionRequired", "La decisión de moderación es obligatoria."));
        }

        if (string.IsNullOrWhiteSpace(contentContext))
        {
            return Result.Failure<ModerationAuditRecord>(
                Error.Validation("ModerationAudit.ContextRequired", "El contexto de la evaluación es obligatorio."));
        }

        if (string.IsNullOrWhiteSpace(actorId))
        {
            return Result.Failure<ModerationAuditRecord>(
                Error.Validation("ModerationAudit.ActorRequired", "El actor de la decisión es obligatorio."));
        }

        if (retentionDays < MinRetentionDays)
        {
            return Result.Failure<ModerationAuditRecord>(
                Error.Validation("ModerationAudit.RetentionInvalid", "La retención debe ser de al menos un día."));
        }

        IReadOnlyList<string> categories = (flaggedCategories ?? [])
            .Where(category => !string.IsNullOrWhiteSpace(category))
            .Select(category => category.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(MaxFlaggedCategories)
            .ToList();

        IReadOnlyDictionary<string, double> scores = categoryScores is { Count: > 0 }
            ? new Dictionary<string, double>(categoryScores, StringComparer.Ordinal)
            : EmptyScores;

        return Result.Success(new ModerationAuditRecord(
            targetId,
            targetType,
            verdict,
            Truncate(decision.Trim(), MaxDecisionLength),
            Truncate(contentContext.Trim(), MaxContentContextLength),
            categories,
            scores,
            string.IsNullOrWhiteSpace(thresholdApplied) ? null : Truncate(thresholdApplied.Trim(), MaxThresholdLength),
            actorType,
            Truncate(actorId.Trim(), MaxActorIdLength),
            Truncate((correlationId ?? string.Empty).Trim(), MaxCorrelationIdLength),
            createdAtUtc,
            createdAtUtc.AddDays(retentionDays)));
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];

    /// <summary>
    /// Reconstruye una evidencia ya persistida sin revalidar ni regenerar el identificador.
    /// Uso exclusivo de la capa de persistencia.
    /// </summary>
    public static ModerationAuditRecord Rehydrate(
        Guid id,
        Guid targetId,
        ModerationTargetType targetType,
        ModerationVerdict verdict,
        string decision,
        string contentContext,
        IReadOnlyList<string> flaggedCategories,
        IReadOnlyDictionary<string, double> categoryScores,
        string? thresholdApplied,
        ModerationActorType actorType,
        string actorId,
        string correlationId,
        DateTime createdAtUtc,
        DateTime retainUntilUtc)
    {
        ModerationAuditRecord record = new(
            targetId,
            targetType,
            verdict,
            decision,
            contentContext,
            flaggedCategories,
            categoryScores,
            thresholdApplied,
            actorType,
            actorId,
            correlationId,
            createdAtUtc,
            retainUntilUtc)
        {
            Id = id
        };

        return record;
    }
}
