using Sensus.Domain.Entities;

namespace Sensus.Domain.Repositories;

/// <summary>
/// Repositorio para persistir "Me gusta" únicos por usuario y publicación de producto.
/// </summary>
public interface IOfferLikeRepository
{
    Task<bool> ToggleLikeAsync(Guid userId, Guid offerId, DateTime nowUtc, CancellationToken cancellationToken = default);
    Task<int> CountByOfferIdAsync(Guid offerId, CancellationToken cancellationToken = default);
    Task<Dictionary<Guid, int>> CountByOfferIdsAsync(IEnumerable<Guid> offerIds, CancellationToken cancellationToken = default);
    Task<HashSet<Guid>> GetLikedOfferIdsAsync(Guid userId, IEnumerable<Guid> offerIds, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<OfferLike>> GetByUserIdsAsync(IEnumerable<Guid> userIds, int limit, CancellationToken cancellationToken = default);
}
