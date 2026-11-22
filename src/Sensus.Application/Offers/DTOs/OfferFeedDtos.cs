namespace Sensus.Application.Offers.DTOs;

/// <summary>
/// Elemento individual del feed de promociones según <c>specs/042-recomendaciones/contracts/openapi.yaml</c>.
/// </summary>
public sealed record OfferFeedItemDto(
    Guid OfferId,
    string Title,
    string Category,
    string? ImageUrl,
    decimal OriginalPrice,
    decimal FinalPrice,
    int Stock,
    int DistanceMeters,
    DateTime ExpiresAtUtc,
    Guid MerchantId,
    string MerchantName,
    string Slot);

/// <summary>
/// Página del feed de promociones con instantánea para paginación estable (Spec 042).
/// </summary>
public sealed record OfferFeedPageDto(
    IReadOnlyList<OfferFeedItemDto> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages,
    Guid? SnapshotId,
    string RankingVersion);
