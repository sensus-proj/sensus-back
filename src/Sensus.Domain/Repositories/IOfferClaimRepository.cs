using Sensus.Domain.Entities;

namespace Sensus.Domain.Repositories;

/// <summary>
/// Contrato de persistencia para los apartados de oferta con código de un solo uso (spec 033).
/// </summary>
public interface IOfferClaimRepository
{
    Task<OfferClaim?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cuenta los apartados activos asociados a una oferta (FR-035-04, FR-036-04).
    /// </summary>
    Task<int> GetActiveClaimCountAsync(Guid offerId, CancellationToken cancellationToken = default);

    Task<OfferClaim?> GetActiveByUserAndOfferAsync(
        Guid userId,
        Guid offerId,
        CancellationToken cancellationToken = default);

    Task<OfferClaim?> GetByCodeHashAsync(string codeHash, CancellationToken cancellationToken = default);

    /// <summary>
    /// Inserta el apartado. Devuelve <c>false</c> si ya existe un apartado activo para el usuario y
    /// la oferta, sin lanzar excepción (FR-033-03).
    /// </summary>
    Task<bool> TryAddAsync(OfferClaim claim, CancellationToken cancellationToken = default);

    Task UpdateAsync(OfferClaim claim, CancellationToken cancellationToken = default);

    /// <summary>
    /// Canje atómico: solo transiciona de Active a Redeemed si sigue vigente y pertenece al comercio.
    /// Devuelve <c>false</c> si otro proceso lo canjeó o expiró primero (FR-033-07, FR-033-08).
    /// </summary>
    Task<bool> TryRedeemAsync(
        Guid claimId,
        Guid merchantId,
        DateTime nowUtc,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Marca como expirados los apartados activos cuya vigencia terminó y devuelve los afectados
    /// para liberar su stock (FR-033-05, NFR-033-03).
    /// </summary>
    Task<IReadOnlyList<OfferClaim>> ExpireClaimsAsync(
        DateTime nowUtc,
        CancellationToken cancellationToken = default);
}
