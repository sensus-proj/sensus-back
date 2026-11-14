using Sensus.Domain.Entities;
using Sensus.Domain.Enums;
using Sensus.Domain.Models;

namespace Sensus.Domain.Repositories;

/// <summary>
/// Contrato de repositorio para la entidad de dominio Offer con soporte para MongoDB Atlas y control de expiración masiva (SE-022 a SE-026).
/// </summary>
public interface IOfferRepository
{
    Task<Offer?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Offer>> GetByMerchantIdAsync(Guid merchantId, CancellationToken cancellationToken = default);
    Task AddAsync(Offer offer, CancellationToken cancellationToken = default);
    Task UpdateAsync(Offer offer, CancellationToken cancellationToken = default);

    /// <summary>
    /// Actualiza la oferta verificando que la versión coincida para concurrencia optimista (FR-035-05).
    /// Devuelve <c>false</c> si la versión en la base de datos no coincide o la oferta no existe.
    /// </summary>
    Task<bool> UpdateWithVersionCheckAsync(
        Offer offer,
        int expectedVersion,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Transición masiva y atómica en MongoDB para expirar ofertas cuya fecha de fin ya transcurrió (Sustituto de Trigger/SQL Job).
    /// </summary>
    Task<int> ExpireOffersAsync(DateTime nowUtc, CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtiene las ofertas visibles dentro de un radio (metros), ordenadas por distancia y fecha de
    /// publicación, filtrando opcionalmente por la categoría del comercio (FR-007-01 a FR-007-07).
    /// </summary>
    Task<NearbyOffersPage> GetNearbyAsync(
        double latitude,
        double longitude,
        int radiusMeters,
        MerchantCategory? category,
        int pageNumber,
        int pageSize,
        DateTime nowUtc,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reserva atómica de una unidad de stock solo si la oferta sigue publicada, vigente y con stock
    /// (FR-033-01, FR-033-02, NFR-033-02).
    /// </summary>
    Task<bool> TryReserveStockAsync(
        Guid offerId,
        DateTime nowUtc,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Devuelve unidades al inventario tras expirar o no completar un apartado (FR-033-05).
    /// </summary>
    Task<bool> ReturnStockAsync(
        Guid offerId,
        int units,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Incrementa atómicamente el contador de vistas de la oferta (FR-032-03, CA-032-03).
    /// </summary>
    Task<bool> IncrementViewCountAsync(
        Guid offerId,
        CancellationToken cancellationToken = default);
}
