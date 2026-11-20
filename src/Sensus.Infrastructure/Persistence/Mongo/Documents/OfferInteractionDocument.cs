using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using Sensus.Domain.Entities;
using Sensus.Domain.Enums;

namespace Sensus.Infrastructure.Persistence.Mongo.Documents;

/// <summary>
/// Documento MongoDB de la colección <c>offer_interactions</c> (Spec 055).
/// No contiene <c>userId</c> en claro, coordenadas, correo ni texto libre (NFR-055-02).
/// </summary>
[BsonIgnoreExtraElements]
public sealed class OfferInteractionDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public Guid Id { get; set; }

    [BsonElement("type")]
    public string Type { get; set; } = string.Empty;

    [BsonElement("pseudoUserId")]
    public string PseudoUserId { get; set; } = string.Empty;

    [BsonElement("keyVersion")]
    public int KeyVersion { get; set; }

    [BsonElement("snapshotId")]
    [BsonIgnoreIfNull]
    public string? SnapshotId { get; set; }

    [BsonElement("rankingVersion")]
    public string RankingVersion { get; set; } = string.Empty;

    [BsonElement("page")]
    public int Page { get; set; }

    [BsonElement("items")]
    public List<OfferInteractionItemDocument> Items { get; set; } = [];

    [BsonElement("occurredAtUtc")]
    public DateTime OccurredAtUtc { get; set; }

    public static OfferInteractionDocument FromDomain(OfferInteraction entity) =>
        new()
        {
            Id = entity.Id,
            Type = entity.Type.ToWireName(),
            PseudoUserId = entity.PseudoUserId,
            KeyVersion = entity.KeyVersion,
            SnapshotId = entity.SnapshotId,
            RankingVersion = entity.RankingVersion,
            Page = entity.Page,
            Items = entity.Items.Select(OfferInteractionItemDocument.FromDomain).ToList(),
            OccurredAtUtc = entity.OccurredAtUtc
        };

    public OfferInteraction ToDomain() =>
        OfferInteraction.Reconstitute(
            Id,
            OfferInteractionCatalog.ParseType(Type),
            PseudoUserId,
            KeyVersion,
            SnapshotId,
            RankingVersion,
            Page,
            Items.Select(i => i.ToDomain()),
            DateTime.SpecifyKind(OccurredAtUtc, DateTimeKind.Utc));
}

[BsonIgnoreExtraElements]
public sealed class OfferInteractionItemDocument
{
    [BsonElement("offerId")]
    [BsonRepresentation(BsonType.String)]
    public Guid OfferId { get; set; }

    [BsonElement("position")]
    public int Position { get; set; }

    [BsonElement("slot")]
    public string Slot { get; set; } = string.Empty;

    [BsonElement("category")]
    public string Category { get; set; } = OfferInteractionItem.UncategorizedCategory;

    [BsonElement("distanceBucket")]
    public string DistanceBucket { get; set; } = string.Empty;

    public static OfferInteractionItemDocument FromDomain(OfferInteractionItem item) =>
        new()
        {
            OfferId = item.OfferId,
            Position = item.Position,
            Slot = item.Slot.ToWireName(),
            Category = item.Category,
            DistanceBucket = item.DistanceBucket.ToWireName()
        };

    public OfferInteractionItem ToDomain() =>
        OfferInteractionItem.Reconstitute(
            OfferId,
            Position,
            OfferInteractionCatalog.ParseSlot(Slot),
            Category,
            OfferInteractionCatalog.ParseDistanceBucket(DistanceBucket));
}
