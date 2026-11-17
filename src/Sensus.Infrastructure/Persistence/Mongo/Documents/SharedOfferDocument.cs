using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using Sensus.Domain.Entities;

namespace Sensus.Infrastructure.Persistence.Mongo.Documents;

/// <summary>
/// Documento de oferta compartida entre amigos en MongoDB Atlas.
/// </summary>
public sealed class SharedOfferDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public Guid Id { get; set; }

    [BsonElement("senderUserId")]
    [BsonRepresentation(BsonType.String)]
    public Guid SenderUserId { get; set; }

    [BsonElement("recipientUserId")]
    [BsonRepresentation(BsonType.String)]
    public Guid RecipientUserId { get; set; }

    [BsonElement("offerId")]
    [BsonRepresentation(BsonType.String)]
    public Guid OfferId { get; set; }

    [BsonElement("offerTitle")]
    public string OfferTitle { get; set; } = string.Empty;

    [BsonElement("storeName")]
    public string StoreName { get; set; } = string.Empty;

    [BsonElement("merchantId")]
    [BsonRepresentation(BsonType.String)]
    public Guid? MerchantId { get; set; }

    [BsonElement("category")]
    public string? Category { get; set; }

    [BsonElement("originalPrice")]
    public decimal OriginalPrice { get; set; }

    [BsonElement("discountedPrice")]
    public decimal DiscountedPrice { get; set; }

    [BsonElement("discountPercentage")]
    public int DiscountPercentage { get; set; }

    [BsonElement("imageUrl")]
    public string? ImageUrl { get; set; }

    [BsonElement("sharedAtUtc")]
    public DateTime SharedAtUtc { get; set; }

    public static SharedOfferDocument FromDomain(SharedOffer entity) =>
        new()
        {
            Id = entity.Id,
            SenderUserId = entity.SenderUserId,
            RecipientUserId = entity.RecipientUserId,
            OfferId = entity.OfferId,
            OfferTitle = entity.OfferTitle,
            StoreName = entity.StoreName,
            MerchantId = entity.MerchantId,
            Category = entity.Category,
            OriginalPrice = entity.OriginalPrice,
            DiscountedPrice = entity.DiscountedPrice,
            DiscountPercentage = entity.DiscountPercentage,
            ImageUrl = entity.ImageUrl,
            SharedAtUtc = entity.SharedAtUtc
        };

    public SharedOffer ToDomain() =>
        SharedOffer.Rehydrate(
            Id,
            SenderUserId,
            RecipientUserId,
            OfferId,
            OfferTitle,
            StoreName,
            MerchantId,
            Category,
            OriginalPrice,
            DiscountedPrice,
            DiscountPercentage,
            ImageUrl,
            SharedAtUtc);
}
