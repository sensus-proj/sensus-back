using Sensus.Domain.Entities;

namespace Sensus.Domain.Repositories;

/// <summary>
/// Repositorio de comentarios de publicaciones del feed (Spec 025).
/// </summary>
public interface IFeedCommentRepository
{
    Task AddAsync(FeedComment comment, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FeedComment>> GetByPostIdPagedAsync(
        Guid postId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<int> CountByPostIdAsync(Guid postId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Anonimiza en lote los comentarios de un autor al dar de baja la cuenta (Spec 061, FR-061-02).
    /// Devuelve el número de documentos modificados; los ya anonimizados se ignoran.
    /// </summary>
    Task<int> AnonymizeByAuthorAsync(
        Guid authorUserId,
        CancellationToken cancellationToken = default);
}
