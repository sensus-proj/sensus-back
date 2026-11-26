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

    /// <summary>
    /// Sincroniza las coordenadas geográficas de las ofertas existentes de un comercio cuando este
    /// actualiza su ubicación.
    /// </summary>
    Task UpdateMerchantLocationAsync(
        Guid merchantId,
        double latitude,
        double longitude,
        CancellationToken cancellationToken = default) => Task.CompletedTask;

    /// <summary>
    /// Búsqueda de ofertas activas con stock y vigencia, filtrando opcionalmente por texto (título/descripción),
    /// categoría y presupuesto máximo (spec 041, FR-046-06).
    /// </summary>
    Task<IReadOnlyList<Offer>> SearchAsync(
        string? query,
        MerchantCategory? category,
        decimal? maxBudgetMxn,
        int limit,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtiene ofertas paginadas de un comercio con filtro opcional por estado (spec 037, FR-046-08).
    /// </summary>
    Task<(IReadOnlyList<Offer> Items, int TotalCount)> GetByMerchantIdPagedAsync(
        Guid merchantId,
        OfferStatus? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtiene candidatas visibles para el feed dentro del radio geográfico excluyendo comercios del usuario
    /// (Spec 042, FR-042-01, FR-042-05).
    /// </summary>
    Task<IReadOnlyList<FeedOfferCandidate>> GetFeedCandidatesAsync(
        double latitude,
        double longitude,
        int radiusMeters,
        Guid excludeMerchantOwnerUserId,
        int maxCandidates,
        DateTime nowUtc,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<FeedOfferCandidate>>(Array.Empty<FeedOfferCandidate>());

    /// <summary>
    /// Revalida visibilidad y datos vigentes de un conjunto de ofertas pertenecientes a una instantánea en Redis
    /// (Spec 042, FR-042-05, FR-042-08).
    /// </summary>
    Task<IReadOnlyList<FeedOfferCandidate>> GetFeedCandidatesByIdsAsync(
        IReadOnlyCollection<Guid> offerIds,
        double referenceLatitude,
        double referenceLongitude,
        Guid excludeMerchantOwnerUserId,
        DateTime nowUtc,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<FeedOfferCandidate>>(Array.Empty<FeedOfferCandidate>());

    /// <summary>
    /// Búsqueda híbrida geoespacial: combina índice de texto en español (stemming) y prefijo normalizado
    /// sobre títulos, o fallback a ofertas cercanas cuando la consulta está vacía (Spec 041).
    /// </summary>
    Task<IReadOnlyList<OfferSearchCandidate>> SearchOffersHybridAsync(
        string? normalizedQuery,
        string? rawQuery,
        double latitude,
        double longitude,
        int radiusMeters,
        int maxCandidates,
        DateTime nowUtc,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<OfferSearchCandidate>>(Array.Empty<OfferSearchCandidate>());

    /// <summary>
    /// Obtiene la oferta origen validada como visible y las alternativas candidatas de la misma categoría
    /// dentro del radio máximo y con precio final comparable &lt;= ratio * precio final original (Spec 043).
    /// </summary>
    Task<OfferAlternativesLookupResult> GetAlternativesAsync(
        Guid offerId,
        int maxDistanceMeters,
        decimal comparablePriceRatio,
        DateTime nowUtc,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(OfferAlternativesLookupResult.NotFoundOrUnavailable());

    /// <summary>
    /// Cuenta las ofertas publicadas y vigentes (<c>EndsAt &gt; nowUtc</c>) de un comercio (FR-034-05).
    /// </summary>
    Task<long> CountActiveByMerchantAsync(
        Guid merchantId,
        DateTime nowUtc,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(0L);

    /// <summary>
    /// Obtiene ofertas en <c>PendingModeration</c> elegibles para reintento (Spec 034, FR-034-07).
    /// </summary>
    Task<IReadOnlyList<Offer>> GetPendingModerationDueAsync(
        DateTime nowUtc,
        int batchSize,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Offer>>(Array.Empty<Offer>());
}
