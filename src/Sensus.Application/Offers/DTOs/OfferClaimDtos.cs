namespace Sensus.Application.Offers.DTOs;

/// <summary>
/// Resultado de crear un apartado (FR-033-04).
/// </summary>
public sealed record ClaimOfferResultDto(
    Guid ClaimId,
    Guid OfferId,
    string Code,
    string QrPayload,
    DateTime ExpiresAtUtc);

/// <summary>
/// Resultado del canje de un apartado por el comerciante propietario (FR-033-06, FR-033-07).
/// </summary>
public sealed record RedeemClaimResultDto(
    Guid ClaimId,
    Guid OfferId,
    Guid MerchantId,
    DateTime RedeemedAtUtc);

/// <summary>
/// Resultado del proceso de expiración y liberación de stock (FR-033-05).
/// </summary>
public sealed record ExpireClaimsResultDto(
    int ExpiredCount,
    int ReturnedUnits,
    DateTime ExecutedAtUtc);
