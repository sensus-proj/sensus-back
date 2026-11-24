using Sensus.Domain.Common;
using Sensus.Domain.Enums;

namespace Sensus.Domain.Entities;

/// <summary>
/// Registro auditable persistido de una transición de estado de oferta (Spec 036 - FR-036-06).
/// </summary>
public sealed class OfferStatusTransition : BaseEntity
{
    public Guid OfferId { get; private set; }
    public OfferStatus FromStatus { get; private set; }
    public OfferStatus ToStatus { get; private set; }
    public Guid? ActorUserId { get; private set; }
    public string TransitionType { get; private set; } = string.Empty;
    public DateTime OccurredAtUtc { get; private set; }

    private OfferStatusTransition()
    {
    }

    public OfferStatusTransition(
        Guid offerId,
        OfferStatus fromStatus,
        OfferStatus toStatus,
        Guid? actorUserId,
        string transitionType,
        DateTime occurredAtUtc,
        Guid? id = null) : base(id ?? Guid.NewGuid())
    {
        OfferId = offerId;
        FromStatus = fromStatus;
        ToStatus = toStatus;
        ActorUserId = actorUserId;
        TransitionType = string.IsNullOrWhiteSpace(transitionType) ? "Manual" : transitionType.Trim();
        OccurredAtUtc = occurredAtUtc.Kind == DateTimeKind.Utc
            ? occurredAtUtc
            : DateTime.SpecifyKind(occurredAtUtc, DateTimeKind.Utc);
    }
}
