namespace Sensus.Application.Offers.DTOs;

/// <summary>
/// DTO de salida del inventario del comerciante (Spec 037). Mantiene la forma plana de
/// <see cref="OfferDto"/> y añade las métricas agregadas de apartados, canjes y vistas.
/// Los campos de "me gusta" se conservan para no romper el contrato de la Spec 057.
/// </summary>
public sealed record MerchantOfferDto(
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
    int Version,
    string? Category,
    int LikesCount,
    bool IsLikedByMe,
    int ActiveClaimsCount,
    int RedemptionsCount,
    int ViewsCount)
    : OfferDto(
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
        Status,
        ImageUrl,
        Latitude,
        Longitude,
        CreatedAtUtc,
        LastModifiedAtUtc,
        Version,
        Category,
        LikesCount,
        IsLikedByMe);
