namespace Sensus.Application.Offers.DTOs;

/// <summary>
/// DTO de una oferta favorita visible del usuario autenticado (Spec 038, FR-038-07).
/// </summary>
public sealed record FavoriteOfferDto(
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
    double? Latitude = null,
    double? Longitude = null,
    int LikesCount = 0,
    DateTime FavoritedAtUtc = default);
