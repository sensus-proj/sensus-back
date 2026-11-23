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
    /// Anonimiza en lote las publicaciones de un autor al dar de baja la cuenta (Spec 061, FR-061-01).
    /// Devuelve el número de documentos modificados; los ya anonimizados se ignoran.
    /// </summary>
    Task<int> AnonymizeByAuthorAsync(
        Guid authorUserId,
        DateTime updatedAtUtc,
        CancellationToken cancellationToken = default);
}
