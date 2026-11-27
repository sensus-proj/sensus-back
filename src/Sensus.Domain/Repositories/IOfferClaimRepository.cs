using Sensus.Domain.Entities;
using Sensus.Domain.Enums;
using Sensus.Domain.Models;

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

    /// <summary>
    /// Cuenta apartados activos y canjeados de varias ofertas en una sola consulta (Spec 037, FR-037-03
    /// y NFR-037-02). La implementación por defecto evita obligar a otros adaptadores a implementar el
    /// conteo batch; los documentos con claves ausentes devuelven <see cref="OfferClaimCounts.Empty"/>.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, OfferClaimCounts>> CountByOfferIdsAsync(
        IEnumerable<Guid> offerIds,
        CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyDictionary<Guid, OfferClaimCounts>>(
                new Dictionary<Guid, OfferClaimCounts>());

    Task<OfferClaim?> GetActiveByUserAndOfferAsync(
        Guid userId,
        Guid offerId,
        CancellationToken cancellationToken = default);

    Task<OfferClaim?> GetActiveByUserAndAnyOfferAsync(
        Guid userId,
        IEnumerable<Guid> offerIds,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<OfferClaim?>(null);

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

    /// <summary>
    /// Obtiene el historial de apartados de un usuario ordenados por fecha de creación descendente,
    /// filtrando opcionalmente por estado (spec 033, FR-046-07).
    /// </summary>
    Task<IReadOnlyList<OfferClaim>> GetByUserIdAsync(
        Guid userId,
        OfferClaimStatus? status = null,
        int limit = 10,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Expira todos los apartados activos de un comercio antes de archivar sus ofertas (Spec 061,
    /// FR-061-03). Devuelve el número de apartados cuya transición se aplicó.
    /// </summary>
    Task<int> ExpireActiveByMerchantAsync(
        Guid merchantId,
        DateTime nowUtc,
        CancellationToken cancellationToken = default);
}
