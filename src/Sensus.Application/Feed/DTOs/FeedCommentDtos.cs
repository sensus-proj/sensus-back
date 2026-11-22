using Sensus.Domain.Entities;

namespace Sensus.Application.Feed.DTOs;

public sealed record FeedCommentDto(
    Guid Id,
    Guid PostId,
    Guid AuthorUserId,
    string AuthorName,
    string? AuthorPhotoUrl,
    string Content,
    DateTime CreatedAtUtc);

public static class FeedCommentMappingExtensions
{
    public static FeedCommentDto ToDto(this FeedComment comment) =>
        new(
            comment.Id,
            comment.PostId,
            comment.AuthorUserId,
            comment.AuthorDisplayName,
            comment.AuthorPhotoUrl,
            comment.Text,
            comment.CreatedAtUtc);
}
