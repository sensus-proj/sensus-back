namespace Sensus.Application.Offers.DTOs;

/// <summary>
/// DTO de salida para ofertas comerciales (SE-022, SE-026 y Specs 034, 036, 037).
/// </summary>
public record OfferDto(
    Guid Id,
    Guid MerchantId,
    string Title,
    string Description,
    decimal OriginalPrice,
    int DiscountPercentage,
    decimal DiscountedPrice,
    int Stock,
    DateTime StartsAt,
    DateTime EndsAt,
    string Status,
    string? ImageUrl,
    double? Latitude,
    double? Longitude,
    DateTime CreatedAtUtc,
    DateTime? LastModifiedAtUtc,
    int Version = 1,
    string? Category = null,
    int LikesCount = 0,
    bool IsLikedByMe = false);
