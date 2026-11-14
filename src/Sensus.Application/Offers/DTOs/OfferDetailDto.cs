namespace Sensus.Application.Offers.DTOs;

/// <summary>
/// Detalle completo de una oferta visible con los datos públicos del comercio y el indicador de
/// apartado activo (FR-032-01, FR-032-02 y FR-032-05).
/// </summary>
public sealed record OfferDetailDto(
    Guid Id,
    Guid MerchantId,
    string MerchantName,
    string MerchantAddress,
    string Category,
    string Title,
    string Description,
    decimal OriginalPrice,
    int DiscountPercentage,
    decimal DiscountedPrice,
    int Stock,
    string Status,
    string? ImageUrl,
    double? Latitude,
    double? Longitude,
    DateTime StartsAt,
    DateTime EndsAt,
    int ViewCount,
    bool HasActiveClaim,
    DateTime CreatedAtUtc,
    DateTime? LastModifiedAtUtc);
