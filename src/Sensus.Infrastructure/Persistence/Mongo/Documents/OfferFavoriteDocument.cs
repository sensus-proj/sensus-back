using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using Sensus.Domain.Entities;

namespace Sensus.Infrastructure.Persistence.Mongo.Documents;

/// <summary>
/// Documento de favorito de oferta en MongoDB Atlas (Spec 038).
/// </summary>
public sealed class OfferFavoriteDocument
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

    public OfferFavorite ToDomain() => OfferFavorite.Rehydrate(Id, UserId, OfferId, CreatedAtUtc);

    public static OfferFavoriteDocument FromDomain(OfferFavorite favorite) =>
        new()
        {
            Id = favorite.Id,
            UserId = favorite.UserId,
            OfferId = favorite.OfferId,
            CreatedAtUtc = favorite.CreatedAtUtc,
        };
}
