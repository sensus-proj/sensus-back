using Sensus.Domain.Entities;

namespace Sensus.Domain.Repositories;

/// <summary>
/// Contrato de persistencia para publicaciones del feed social (Spec 024).
/// </summary>
public interface IFeedPostRepository
{
    Task AddAsync(FeedPost post, CancellationToken cancellationToken = default);

    Task<FeedPost?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task UpdateAsync(FeedPost post, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FeedPost>> GetPublishedPagedAsync(
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<int> CountPublishedAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Publicaciones publicadas, no eliminadas y con ubicación dentro del radio, de la más reciente a la más
    /// antigua con el identificador como desempate estable (Spec 026, FR-026-01, FR-026-02, FR-026-05, CA-026-07).
    /// Devuelve la página y el total con el mismo filtro.
    /// </summary>
    Task<(IReadOnlyList<FeedPost> Items, int TotalCount)> GetPublishedNearbyPagedAsync(
        double latitude,
        double longitude,
        double radiusMeters,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Anonimiza en lote las publicaciones de un autor al dar de baja la cuenta (Spec 061, FR-061-01).
    /// Devuelve el número de documentos modificados; los ya anonimizados se ignoran.
    /// </summary>
    Task<int> AnonymizeByAuthorAsync(
        Guid authorUserId,
        DateTime updatedAtUtc,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Purga las publicaciones en cuarentena creadas antes de <paramref name="cutoffUtc"/> (Spec 024 / retención 30 días).
    /// </summary>
    Task<int> PurgeQuarantinedBeforeAsync(
        DateTime cutoffUtc,
        CancellationToken cancellationToken = default);
}
