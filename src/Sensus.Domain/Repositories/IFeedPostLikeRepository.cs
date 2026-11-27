using Sensus.Domain.Entities;

namespace Sensus.Domain.Repositories;

/// <summary>
/// Persistencia de los me gusta de publicaciones del feed (Spec 039).
/// Las operaciones son idempotentes: la unicidad publicación + usuario la garantiza el almacenamiento (FR-039-04).
/// </summary>
public interface IFeedPostLikeRepository
{
    /// <summary>
    /// Registra el me gusta. Devuelve <c>true</c> solo si creó el registro;
    /// <c>false</c> si ya existía (petición repetida o concurrente, FR-039-05).
    /// </summary>
    Task<bool> AddAsync(FeedPostLike like, CancellationToken cancellationToken = default);

    /// <summary>
    /// Elimina el me gusta. Devuelve <c>true</c> solo si existía y se eliminó.
    /// </summary>
    Task<bool> RemoveAsync(Guid postId, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Conteo de me gusta de una publicación, derivado de los registros únicos (nunca negativo).
    /// </summary>
    Task<int> CountByPostIdAsync(Guid postId, CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<Guid, int>> CountByPostIdsAsync(
        IEnumerable<Guid> postIds,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyDictionary<Guid, int>>(new Dictionary<Guid, int>());

    Task<IReadOnlySet<Guid>> GetLikedPostIdsByUserAsync(
        Guid userId,
        IEnumerable<Guid> postIds,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlySet<Guid>>(new HashSet<Guid>());
}
