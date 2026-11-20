namespace Sensus.Application.Comercios.DTOs;

public sealed record StoreReviewReplyDto(
    Guid Id,
    Guid AuthorUserId,
    string AuthorName,
    string? AuthorPhotoUrl,
    string Comment,
    DateTime CreatedAtUtc);

public sealed record StoreReviewDto(
    Guid Id,
    Guid MerchantId,
    Guid AuthorUserId,
    string AuthorName,
    string? AuthorPhotoUrl,
    int Rating,
    string Comment,
    DateTime CreatedAtUtc,
    int LikesCount,
    bool IsLikedByMe,
    IReadOnlyList<StoreReviewReplyDto> Replies);

public sealed record RatingBreakdownItemDto(
    int Stars,
    int Count);

public sealed record StoreReviewsSummaryDto(
    Guid MerchantId,
    string MerchantName,
    double AverageRating,
    int TotalReviews,
    IReadOnlyList<RatingBreakdownItemDto> Breakdown,
    IReadOnlyList<StoreReviewDto> Reviews);

public sealed record ReviewLikeStatusDto(
    Guid ReviewId,
    int LikesCount,
    bool IsLikedByMe);
