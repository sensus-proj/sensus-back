using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using Sensus.Domain.Entities;
using Sensus.Domain.Enums;

namespace Sensus.Infrastructure.Persistence.Mongo.Documents;

/// <summary>
/// Documento persistido del historial de cambios de estado de oferta (Spec 036).
/// </summary>
public sealed class OfferStatusHistoryDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public Guid Id { get; set; }

    [BsonElement("offerId")]
    [BsonRepresentation(BsonType.String)]
    public Guid OfferId { get; set; }

    [BsonElement("fromStatus")]
    public string FromStatus { get; set; } = string.Empty;

    [BsonElement("toStatus")]
    public string ToStatus { get; set; } = string.Empty;

    [BsonElement("actorUserId")]
    [BsonRepresentation(BsonType.String)]
    public Guid? ActorUserId { get; set; }

    [BsonElement("transitionType")]
    public string TransitionType { get; set; } = string.Empty;

    [BsonElement("occurredAtUtc")]
    public DateTime OccurredAtUtc { get; set; }

    public OfferStatusTransition ToDomain()
    {
        Enum.TryParse<OfferStatus>(FromStatus, true, out OfferStatus from);
        Enum.TryParse<OfferStatus>(ToStatus, true, out OfferStatus to);
        return new OfferStatusTransition(
            OfferId,
            from,
            to,
            ActorUserId,
            TransitionType,
            OccurredAtUtc,
            Id);
    }

    public static OfferStatusHistoryDocument FromDomain(OfferStatusTransition transition) =>
        new()
        {
            Id = transition.Id,
            OfferId = transition.OfferId,
            FromStatus = transition.FromStatus.ToString(),
            ToStatus = transition.ToStatus.ToString(),
            ActorUserId = transition.ActorUserId,
            TransitionType = transition.TransitionType,
            OccurredAtUtc = transition.OccurredAtUtc
        };
}
