using Sensus.Application.Comercios.DTOs;
using Sensus.Domain.Entities;

namespace Sensus.Application.Comercios;

public static class StoreReviewMappingExtensions
{
    public static StoreReviewDto ToDto(this StoreReview review, Guid? currentUserId)
    {
        var replies = review.Replies
            .OrderBy(r => r.CreatedAtUtc)
            .Select(r => new StoreReviewReplyDto(
                r.Id,
                r.AuthorUserId,
                r.AuthorName,
                r.AuthorPhotoUrl,
                r.Comment,
                r.CreatedAtUtc))
            .ToList();

        bool isLikedByMe = currentUserId.HasValue
            && currentUserId.Value != Guid.Empty
            && review.LikedByUserIds.Contains(currentUserId.Value);

        return new StoreReviewDto(
            review.Id,
            review.MerchantId,
            review.AuthorUserId,
            review.AuthorName,
            review.AuthorPhotoUrl,
            review.Rating,
            review.Comment,
            review.CreatedAtUtc,
            review.LikedByUserIds.Count,
            isLikedByMe,
            replies);
    }

    public static StoreReviewsSummaryDto BuildSummary(
        Comercio comercio,
        IReadOnlyList<StoreReview> reviews,
        IReadOnlyDictionary<int, int> ratings,
        Guid? currentUserId,
        int page,
        int pageSize)
    {
        int total = ratings.Values.Sum();
        double avg = total == 0 ? 0.0 : Math.Round(ratings.Sum(rating => (long)rating.Key * rating.Value) / (double)total, 1);

        var breakdown = Enumerable.Range(1, StoreReview.MaxRating)
            .Reverse()
            .Select(star => new RatingBreakdownItemDto(star, ratings.GetValueOrDefault(star, 0)))
            .ToList();

        var orderedReviews = reviews
            .OrderByDescending(r => r.CreatedAtUtc)
            .Select(r => r.ToDto(currentUserId))
            .ToList();

        return new StoreReviewsSummaryDto(
            comercio.Id,
            comercio.NombreComercial,
            avg,
            total,
            breakdown,
            orderedReviews,
            page,
            pageSize);
    }
}
