namespace Sensus.Application.Offers.DTOs;

public sealed record OfferClaimItemDto(
    Guid OfferId,
    int Quantity,
    string? OfferTitle = null,
    decimal? UnitPrice = null);

public sealed record OfferClaimDetailDto(
    Guid ClaimId,
    Guid OfferId,
    string Status,
    DateTime ExpiresAtUtc,
    string? QrPayload,
    int TotalUnits = 1,
    IReadOnlyList<OfferClaimItemDto>? Items = null);

/// <summary>
/// Resultado de crear un apartado (FR-033-04).
/// </summary>
public sealed record ClaimOfferResultDto(
    Guid ClaimId,
    Guid OfferId,
    string Code,
    string QrPayload,
    DateTime ExpiresAtUtc,
    int TotalUnits = 1,
    IReadOnlyList<OfferClaimItemDto>? Items = null);

/// <summary>
/// Resultado del canje de un apartado por el comerciante propietario (FR-033-06, FR-033-07).
/// </summary>
public sealed record RedeemClaimResultDto(
    Guid ClaimId,
    Guid OfferId,
    Guid MerchantId,
    DateTime RedeemedAtUtc,
    int TotalUnits = 1,
    IReadOnlyList<OfferClaimItemDto>? Items = null);

/// <summary>
/// Resultado del proceso de expiración y liberación de stock (FR-033-05).
/// </summary>
public sealed record ExpireClaimsResultDto(
    int ExpiredCount,
    int ReturnedUnits,
    DateTime ExecutedAtUtc);
