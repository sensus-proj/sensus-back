using Sensus.Domain.Common;
using Sensus.Domain.Enums;
using Sensus.Domain.ValueObjects;

namespace Sensus.Domain.Entities;

/// <summary>
/// Sanción aplicada a un usuario (Spec 020). Es un registro de auditoría de solo adición: nunca se elimina y la
/// reversión conserva el historial (NFR-020-02). El estado <c>Expired</c> se deriva de <see cref="EndsAtUtc"/>.
/// </summary>
public sealed class Sanction : BaseEntity, IAggregateRoot
{
    public const string SystemActor = "system";
    public const int MaxReasonLength = 500;

    public Guid UserId { get; private set; }
    public SanctionType Type { get; private set; }
    public string Reason { get; private set; } = string.Empty;
    public SanctionEvidence Evidence { get; private set; } = SanctionEvidence.Manual;
    public string AppliedBy { get; private set; } = SystemActor;
    public DateTime StartsAtUtc { get; private set; }
    public DateTime? EndsAtUtc { get; private set; }
    public Guid? RevertedBy { get; private set; }
    public DateTime? RevertedAtUtc { get; private set; }
    public string? RevertReason { get; private set; }

    private Sanction() : base()
    {
    }

    private Sanction(Guid id) : base(id)
    {
    }

    public bool IsPermanent => EndsAtUtc is null;

    /// <summary>Crea una sanción automática originada por la moderación (responsable <c>system</c>).</summary>
    public static Result<Sanction> CreateAutomatic(
        Guid userId,
        string reason,
        SanctionEvidence evidence,
        DateTime startsAtUtc,
        int? durationHours) =>
        Create(userId, SanctionType.Automatic, reason, evidence, SystemActor, startsAtUtc, durationHours);

    /// <summary>Crea una sanción manual aplicada por un administrador.</summary>
    public static Result<Sanction> CreateManual(
        Guid userId,
        Guid adminId,
        string reason,
        DateTime startsAtUtc,
        int? durationHours) =>
        Create(
            userId,
            SanctionType.Manual,
            reason,
            SanctionEvidence.Manual,
            adminId.ToString("D"),
            startsAtUtc,
            durationHours);

    /// <summary>Estado efectivo de la sanción en el instante indicado.</summary>
    public SanctionState GetState(DateTime nowUtc)
    {
        if (RevertedAtUtc is not null)
        {
            return SanctionState.Reverted;
        }

        return EndsAtUtc is not null && EndsAtUtc <= nowUtc ? SanctionState.Expired : SanctionState.Active;
    }

    public bool IsActive(DateTime nowUtc) => GetState(nowUtc) == SanctionState.Active;

    /// <summary>
    /// Revierte la sanción. Solo una sanción activa puede revertirse; el motivo es obligatorio.
    /// </summary>
    public Result Revert(Guid adminId, string reason, DateTime nowUtc)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return Result.Failure(Error.Validation("Sanction.RevertReasonRequired", "El motivo de la reversión es obligatorio."));
        }

        if (reason.Trim().Length > MaxReasonLength)
        {
            return Result.Failure(Error.Validation("Sanction.ReasonTooLong", $"El motivo no puede superar {MaxReasonLength} caracteres."));
        }

        if (!IsActive(nowUtc))
        {
            return Result.Failure(Error.Conflict("Sanction.NotActive", "La sanción ya fue revertida o venció."));
        }

        RevertedBy = adminId;
        RevertedAtUtc = nowUtc;
        RevertReason = reason.Trim();
        return Result.Success();
    }

    /// <summary>
    /// Reconstruye una sanción persistida. Uso exclusivo de la capa de persistencia.
    /// </summary>
    public static Sanction Rehydrate(
        Guid id,
        Guid userId,
        SanctionType type,
        string reason,
        SanctionEvidence evidence,
        string appliedBy,
        DateTime startsAtUtc,
        DateTime? endsAtUtc,
        Guid? revertedBy,
        DateTime? revertedAtUtc,
        string? revertReason) =>
        new(id)
        {
            UserId = userId,
            Type = type,
            Reason = reason,
            Evidence = evidence,
            AppliedBy = appliedBy,
            StartsAtUtc = startsAtUtc,
            EndsAtUtc = endsAtUtc,
            RevertedBy = revertedBy,
            RevertedAtUtc = revertedAtUtc,
            RevertReason = revertReason
        };

    private static Result<Sanction> Create(
        Guid userId,
        SanctionType type,
        string reason,
        SanctionEvidence evidence,
        string appliedBy,
        DateTime startsAtUtc,
        int? durationHours)
    {
        if (userId == Guid.Empty)
        {
            return Result.Failure<Sanction>(Error.Validation("Sanction.UserRequired", "El usuario sancionado es obligatorio."));
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            return Result.Failure<Sanction>(Error.Validation("Sanction.ReasonRequired", "El motivo de la sanción es obligatorio."));
        }

        if (reason.Trim().Length > MaxReasonLength)
        {
            return Result.Failure<Sanction>(Error.Validation("Sanction.ReasonTooLong", $"El motivo no puede superar {MaxReasonLength} caracteres."));
        }

        if (durationHours is <= 0)
        {
            return Result.Failure<Sanction>(Error.Validation("Sanction.DurationInvalid", "La duración debe ser positiva o nula para un baneo permanente."));
        }

        var sanction = new Sanction(Guid.NewGuid())
        {
            UserId = userId,
            Type = type,
            Reason = reason.Trim(),
            Evidence = evidence,
            AppliedBy = appliedBy,
            StartsAtUtc = startsAtUtc,
            EndsAtUtc = durationHours is null ? null : startsAtUtc.AddHours(durationHours.Value)
        };

        return Result.Success(sanction);
    }
}
