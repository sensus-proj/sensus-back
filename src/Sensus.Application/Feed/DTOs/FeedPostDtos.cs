using Sensus.Domain.Entities;

namespace Sensus.Application.Feed.DTOs;

public sealed record FeedPostDto(
    Guid Id,
    Guid AuthorUserId,
    string AuthorDisplayName,
    string? AuthorPhotoUrl,
    string Content,
    string? ImageUrl,
    string Status,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc,
    Guid? MerchantId = null,
    string? MerchantName = null,
    int LikesCount = 0,
    bool IsLikedByMe = false,
    int CommentsCount = 0,
    bool IsMine = false);

public static class FeedPostMappingExtensions
{
    public static FeedPostDto ToDto(this FeedPost post) =>
        new(
            post.Id,
            post.AuthorUserId,
            post.AuthorDisplayName,
            post.AuthorPhotoUrl,
            post.Content,
            post.ImageUrl,
            post.Status.ToString(),
            post.CreatedAtUtc,
            post.UpdatedAtUtc,
            post.MerchantId,
            post.MerchantName,
            IsMine: true);
}
