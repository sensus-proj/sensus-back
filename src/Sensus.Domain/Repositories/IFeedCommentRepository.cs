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
}
