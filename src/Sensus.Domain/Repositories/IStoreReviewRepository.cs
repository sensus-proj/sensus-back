using Sensus.Domain.Entities;

namespace Sensus.Domain.Repositories;

/// <summary>
/// Repositorio de reseñas de comercios con soporte para likes, respuestas y promedio agregado.
/// </summary>
public interface IStoreReviewRepository
{
    Task<StoreReview?> GetByIdAsync(Guid reviewId, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<StoreReview> Reviews, IReadOnlyDictionary<int, int> Ratings)> GetMerchantPageAsync(
        Guid merchantId, int page, int pageSize, CancellationToken cancellationToken = default);
    Task AddAsync(StoreReview review, CancellationToken cancellationToken = default);
    Task<bool> AddReplyAsync(Guid reviewId, StoreReviewReply reply, CancellationToken cancellationToken = default);
    Task<(bool IsLiked, int Count)?> ToggleLikeAsync(Guid reviewId, Guid userId, CancellationToken cancellationToken = default);
}
