using Sensus.Application.Common.Interfaces;
using Sensus.Domain.Common;
using Sensus.Domain.Enums;

namespace Sensus.Application.Common.Moderation;

/// <summary>
/// Resultado detallado de una evaluación preventiva de texto (Spec 021).
/// Permite auditar el veredicto, las categorías marcadas y los puntajes del motor.
/// Ref: FR-021-01 - veredicto, puntajes y umbral de cada decisión.
/// </summary>
public sealed record ModerationEvaluation(
    bool IsApproved,
    string Decision,
    string? Message,
    IReadOnlyList<string> FlaggedCategories,
    IReadOnlyDictionary<string, double>? CategoryScores,
    Error? RejectionError)
{
    /// <summary>Traduce la decisión textual del motor al veredicto auditado.</summary>
    public ModerationVerdict Verdict => ContentModerationHelper.ToVerdict(Decision);
}

/// <summary>
/// Helper compartido para moderación preventiva (fail-closed) de textos de usuario, comercio y ofertas.
/// </summary>
public static class ContentModerationHelper
{
    /// <summary>Decisión que el motor usa para contenido que requiere revisión manual.</summary>
    public const string QuarantineDecision = "Quarantine";

    /// <summary>Decisión que el motor usa para contenido apto.</summary>
    public const string ApprovedDecision = "Approved";

    /// <summary>Decisión que el motor usa para contenido rechazado.</summary>
    public const string RejectedDecision = "Rejected";

    private const string ModerationUnavailableMessage =
        "La moderación no está disponible; el contenido no fue publicado.";

    /// <summary>
    /// Evalúa el texto y devuelve el detalle completo de la decisión, sin descartar categorías ni puntajes.
    /// Ref: FR-021-01 - evidencia necesaria para auditar la decisión.
    /// Ref: FR-017-09 - comportamiento fail-closed cuando no existe o falla el servicio de moderación.
    /// </summary>
    public static async Task<ModerationEvaluation> EvaluateAsync(
        ITextModerationService? moderationService,
        string? text,
        string contentContext,
        string errorCode,
        string defaultRejectionMessage,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Approved(ApprovedDecision, "Texto vacío aprobado.");
        }

        if (ProfanityFilter.ContainsProfanity(text))
        {
            return Rejected(
                RejectedDecision,
                ProfanityFilter.DefaultRejectionMessage,
                [],
                null,
                Error.Validation(errorCode, ProfanityFilter.DefaultRejectionMessage));
        }

        if (moderationService is null)
        {
            return UnavailableQuarantine(errorCode);
        }

        TextModerationResult? verdict;
        try
        {
            verdict = await moderationService.ModerateContentTextAsync(
                text,
                contentContext,
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return UnavailableQuarantine(errorCode);
        }

        if (verdict is null)
        {
            return UnavailableQuarantine(errorCode);
        }

        if (verdict is { IsApproved: true })
        {
            return new ModerationEvaluation(
                true,
                string.IsNullOrWhiteSpace(verdict.Decision) ? ApprovedDecision : verdict.Decision,
                verdict.Message,
                verdict.FlaggedCategories ?? [],
                verdict.CategoryScores,
                null);
        }

        string message = !string.IsNullOrWhiteSpace(verdict.Message)
            ? verdict.Message
            : defaultRejectionMessage;

        string decision = !string.IsNullOrWhiteSpace(verdict.Decision)
            ? verdict.Decision
            : RejectedDecision;

        return new ModerationEvaluation(
            false,
            decision,
            message,
            verdict.FlaggedCategories ?? [],
            verdict.CategoryScores,
            Error.Validation(errorCode, message));
    }

    /// <summary>
    /// Versión booleana compatible con los flujos existentes: falla con el error de rechazo cuando el texto
    /// no es apto.
    /// </summary>
    public static async Task<Result> EnsureApprovedAsync(
        ITextModerationService? moderationService,
        string? text,
        string contentContext,
        string errorCode,
        string defaultRejectionMessage,
        CancellationToken cancellationToken)
    {
        ModerationEvaluation evaluation = await EvaluateAsync(
            moderationService,
            text,
            contentContext,
            errorCode,
            defaultRejectionMessage,
            cancellationToken);

        return evaluation.IsApproved
            ? Result.Success()
            : Result.Failure(evaluation.RejectionError ?? Error.Validation(errorCode, defaultRejectionMessage));
    }

    /// <summary>Traduce la decisión textual del motor al veredicto auditado.</summary>
    public static ModerationVerdict ToVerdict(string? decision) =>
        decision?.Trim().ToLowerInvariant() switch
        {
            "approved" => ModerationVerdict.Approved,
            "quarantine" => ModerationVerdict.Quarantined,
            _ => ModerationVerdict.Rejected
        };

    private static ModerationEvaluation Approved(string decision, string message) =>
        new(true, decision, message, [], null, null);

    private static ModerationEvaluation UnavailableQuarantine(string errorCode) =>
        Rejected(
            QuarantineDecision,
            ModerationUnavailableMessage,
            [],
            null,
            Error.Failure($"{errorCode}.ModerationUnavailable", ModerationUnavailableMessage));

    private static ModerationEvaluation Rejected(
        string decision,
        string message,
        IReadOnlyList<string> flaggedCategories,
        IReadOnlyDictionary<string, double>? categoryScores,
        Error error) =>
        new(false, decision, message, flaggedCategories, categoryScores, error);
}
