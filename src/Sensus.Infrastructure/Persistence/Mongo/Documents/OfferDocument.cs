using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver.GeoJsonObjectModel;
using Sensus.Domain.Entities;
using Sensus.Domain.Enums;

namespace Sensus.Infrastructure.Persistence.Mongo.Documents;

/// <summary>
/// Documento de oferta en MongoDB Atlas con soporte geoespacial e índices para vigencia (SE-022 a SE-026).
/// </summary>
public sealed class OfferDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public Guid Id { get; set; }

    [BsonRepresentation(BsonType.String)]
    [BsonElement("merchantId")]
    public Guid MerchantId { get; set; }

    [BsonElement("title")]
    public string Title { get; set; } = string.Empty;

    [BsonElement("description")]
    public string Description { get; set; } = string.Empty;

    [BsonElement("originalPrice")]
    [BsonRepresentation(BsonType.Decimal128)]
    public decimal OriginalPrice { get; set; }

    [BsonElement("discountPercentage")]
    public int DiscountPercentage { get; set; }

    [BsonElement("discountedPrice")]
    [BsonRepresentation(BsonType.Decimal128)]
    public decimal DiscountedPrice { get; set; }

    [BsonElement("stock")]
    public int Stock { get; set; }

    [BsonElement("startsAt")]
    public DateTime StartsAt { get; set; }

    [BsonElement("endsAt")]
    public DateTime EndsAt { get; set; }

    [BsonElement("status")]
    public string Status { get; set; } = OfferStatus.Published.ToString();

    [BsonElement("imageUrl")]
    public string? ImageUrl { get; set; }

    [BsonElement("viewCount")]
    public int ViewCount { get; set; }

    [BsonElement("latitude")]
    public double? Latitude { get; set; }

    [BsonElement("longitude")]
    public double? Longitude { get; set; }

    [BsonElement("ubicacion")]
    public GeoJsonPoint<GeoJson2DGeographicCoordinates>? Ubicacion { get; set; }

    [BsonElement("createdAtUtc")]
    public DateTime CreatedAtUtc { get; set; }

    [BsonElement("lastModifiedAtUtc")]
    public DateTime? LastModifiedAtUtc { get; set; }

    public Offer ToDomain()
    {
        var status = Enum.TryParse<OfferStatus>(Status, true, out var parsedStatus)
            ? parsedStatus
            : OfferStatus.Published;

        return Offer.Reconstitute(
            Id,
            MerchantId,
            Title,
            Description,
            OriginalPrice,
            DiscountPercentage,
            DiscountedPrice,
            Stock,
            StartsAt,
            EndsAt,
            status,
            ImageUrl,
            Latitude,
            Longitude,
            CreatedAtUtc,
            LastModifiedAtUtc,
            ViewCount);
    }

    public static OfferDocument FromDomain(Offer offer)
    {
        GeoJsonPoint<GeoJson2DGeographicCoordinates>? geoPoint = null;
        if (offer.Latitude.HasValue && offer.Longitude.HasValue)
        {
            geoPoint = GeoJson.Point(GeoJson.Geographic(offer.Longitude.Value, offer.Latitude.Value));
        }

        return new OfferDocument
        {
            Id = offer.Id,
            MerchantId = offer.MerchantId,
            Title = offer.Title,
            Description = offer.Description,
            OriginalPrice = offer.OriginalPrice,
            DiscountPercentage = offer.DiscountPercentage,
            DiscountedPrice = offer.DiscountedPrice,
            Stock = offer.Stock,
            StartsAt = offer.StartsAt,
            EndsAt = offer.EndsAt,
            Status = offer.Status.ToString(),
            ImageUrl = offer.ImageUrl,
            Latitude = offer.Latitude,
            Longitude = offer.Longitude,
            ViewCount = offer.ViewCount,
            Ubicacion = geoPoint,
            CreatedAtUtc = offer.CreatedAtUtc,
            LastModifiedAtUtc = offer.LastModifiedAtUtc
        };
    }
}
