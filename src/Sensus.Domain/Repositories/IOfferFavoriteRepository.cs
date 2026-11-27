using Sensus.Domain.Models;

namespace Sensus.Domain.Repositories;

/// <summary>
/// Contrato de persistencia para favoritos de ofertas únicos por usuario (Spec 038).
/// </summary>
public interface IOfferFavoriteRepository
{
    Task<bool> ToggleFavoriteAsync(
        Guid userId,
        Guid offerId,
        DateTime nowUtc,
        CancellationToken cancellationToken = default);

    Task<int> CountByOfferIdAsync(Guid offerId, CancellationToken cancellationToken = default);

    Task<bool> IsFavoritedAsync(Guid userId, Guid offerId, CancellationToken cancellationToken = default);

    Task<HashSet<Guid>> GetFavoritedOfferIdsAsync(
        Guid userId,
        IEnumerable<Guid> offerIds,
        CancellationToken cancellationToken = default);

    Task<FavoriteOffersPage> GetPagedByUserIdAsync(
        Guid userId,
        int pageNumber,
        int pageSize,
        DateTime nowUtc,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new FavoriteOffersPage([], 0));
}
