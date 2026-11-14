namespace Sensus.Application.Offers.DTOs;

/// <summary>
/// DTO de una oferta cercana al usuario, con la distancia calculada y los datos públicos del
/// comercio que la publica (FR-007-03, FR-007-05 y FR-007-07).
/// </summary>
public sealed record NearbyOfferDto(
    Guid Id,
    Guid MerchantId,
    string MerchantName,
    string Category,
    string Title,
    string Description,
    decimal OriginalPrice,
    int DiscountPercentage,
    decimal DiscountedPrice,
    int Stock,
    string? ImageUrl,
    DateTime StartsAt,
    DateTime EndsAt,
    double DistanceMeters,
    double? Latitude = null,
    double? Longitude = null);
