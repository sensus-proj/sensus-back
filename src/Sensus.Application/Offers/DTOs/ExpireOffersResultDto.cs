namespace Sensus.Application.Offers.DTOs;

/// <summary>
/// Resultado del proceso por lotes de expiración de ofertas (Sustituto de Trigger/Job SQL).
/// </summary>
public sealed record ExpireOffersResultDto(
    int ExpiredCount,
    DateTime ExecutedAtUtc);
