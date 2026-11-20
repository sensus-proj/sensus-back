using Sensus.Domain.Entities;

namespace Sensus.Domain.Repositories;

/// <summary>
/// Repositorio de reseñas de comercios con soporte para likes, respuestas y promedio agregado.
/// </summary>
public interface IStoreReviewRepository
{
    Task<StoreReview?> GetByIdAsync(Guid reviewId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<StoreReview>> GetByMerchantIdAsync(Guid merchantId, CancellationToken cancellationToken = default);
    Task<Dictionary<Guid, (double AverageRating, int ReviewCount)>> GetRatingSummariesByMerchantIdsAsync(
        IEnumerable<Guid> merchantIds,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<StoreReview>> GetRecentActivityByUserIdsAsync(
        IEnumerable<Guid> userIds,
        int limit,
        CancellationToken cancellationToken = default);
    Task AddAsync(StoreReview review, CancellationToken cancellationToken = default);
    Task UpdateAsync(StoreReview review, CancellationToken cancellationToken = default);
}
