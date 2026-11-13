namespace Sensus.Application.Offers.DTOs;

/// <summary>
/// DTO de salida para ofertas comerciales (SE-022, SE-026 y Specs 034, 036, 037).
/// </summary>
public sealed record OfferDto(
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
    DateTime? LastModifiedAtUtc);
