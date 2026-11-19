using Sensus.Application.Common.Interfaces;
using Sensus.Application.Common.Settings;
using Sensus.Domain.Common;
using Sensus.Domain.Entities;
using Sensus.Domain.Enums;
using Sensus.Domain.Policies;

namespace Sensus.Application.Sanctions.Common;

public sealed record SanctionEvidenceDto(
    Guid? MediaId,
    string Decision,
    IReadOnlyList<string> Categories,
    IReadOnlyDictionary<string, double> Scores,
    string? ThresholdApplied,
    string Action);

public sealed record SanctionDto(
    Guid Id,
    Guid UserId,
    string Type,
    string Status,
    string Reason,
    SanctionEvidenceDto Evidence,
    string AppliedBy,
    DateTime StartsAtUtc,
    DateTime? EndsAtUtc,
    Guid? RevertedBy,
    DateTime? RevertedAtUtc,
    string? RevertReason);

public static class SanctionMapping
{
    public const string UserSanctionedCode = "Auth.UserSanctioned";
    public const string SanctionEndsAtKey = "sanctionEndsAtUtc";

    public static SanctionDto ToDto(this Sanction sanction, DateTime nowUtc) =>
        new(
            sanction.Id,
            sanction.UserId,
            sanction.Type.ToString(),
            sanction.GetState(nowUtc).ToString(),
            sanction.Reason,
            new SanctionEvidenceDto(
                sanction.Evidence.MediaId,
                sanction.Evidence.Decision,
                sanction.Evidence.Categories,
                sanction.Evidence.Scores,
                sanction.Evidence.ThresholdApplied,
                sanction.Evidence.Action),
            sanction.AppliedBy,
            sanction.StartsAtUtc,
            sanction.EndsAtUtc,
            sanction.RevertedBy,
            sanction.RevertedAtUtc,
            sanction.RevertReason);

    /// <summary>
    /// Error 403 del login para un usuario sancionado (CA-020-03). No expone evidencia, categorías ni motivo.
    /// </summary>
    public static Error ToLoginError(this User user) =>
        Error.Forbidden(
            UserSanctionedCode,
            user.Suspension?.EndsAtUtc is null
                ? "Tu cuenta fue suspendida de forma permanente por incumplir las normas de la comunidad."
                : "Tu cuenta está suspendida temporalmente por incumplir las normas de la comunidad.",
            new Dictionary<string, object?> { [SanctionEndsAtKey] = user.Suspension?.EndsAtUtc });

    public static IReadOnlyList<SanctionStep> ToSteps(this SanctionSettings settings) =>
        settings.Steps
            .Select(step => step.Permanent ? new SanctionStep(null) : new SanctionStep(step.Hours))
            .ToList();

    public static bool TryGetUserId(this ICurrentUserService currentUser, out Guid userId) =>
        Guid.TryParse(currentUser.UserId, out userId) && userId != Guid.Empty;

    public static bool IsAdministrator(this ICurrentUserService currentUser) =>
        currentUser.Roles.Contains(RoleType.Administrador, StringComparer.OrdinalIgnoreCase);
}
