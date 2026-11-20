using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using Sensus.Domain.Entities;

namespace Sensus.Infrastructure.Persistence.Mongo.Documents;

public sealed class OfferLikeDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public Guid Id { get; set; }

    [BsonRepresentation(BsonType.String)]
    [BsonElement("userId")]
    public Guid UserId { get; set; }

    [BsonRepresentation(BsonType.String)]
    [BsonElement("offerId")]
    public Guid OfferId { get; set; }

    [BsonElement("createdAtUtc")]
    public DateTime CreatedAtUtc { get; set; }

    public OfferLike ToDomain() => OfferLike.Rehydrate(Id, UserId, OfferId, CreatedAtUtc);

    public static OfferLikeDocument FromDomain(OfferLike like) =>
        new()
        {
            Id = like.Id,
            UserId = like.UserId,
            OfferId = like.OfferId,
            CreatedAtUtc = like.CreatedAtUtc,
        };
}
