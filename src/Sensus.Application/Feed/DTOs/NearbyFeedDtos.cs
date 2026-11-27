using Sensus.Domain.Entities;

namespace Sensus.Application.Feed.DTOs;

/// <summary>
/// Proyección pública de una publicación del feed cercano (Spec 026).
/// No incluye coordenadas ni estado interno: la ubicación solo sirve para filtrar.
/// </summary>
public sealed record NearbyFeedPostDto(
    Guid Id,
    Guid AuthorUserId,
    string AuthorDisplayName,
    string? AuthorPhotoUrl,
    string Content,
    string? ImageUrl,
    DateTime CreatedAtUtc,
    Guid? MerchantId = null,
    string? MerchantName = null,
    int LikesCount = 0,
    bool IsLikedByMe = false,
    int CommentsCount = 0,
    bool IsMine = false);

/// <summary>Entrada serializable de la caché del feed cercano.</summary>
public sealed record NearbyFeedCacheEntry(List<NearbyFeedPostDto> Items, int TotalCount);

public static class NearbyFeedMappingExtensions
{
    public static NearbyFeedPostDto ToNearbyDto(this FeedPost post) =>
        new(
            post.Id,
            post.AuthorUserId,
            post.AuthorDisplayName,
            post.AuthorPhotoUrl,
            post.Content,
            post.ImageUrl,
            post.CreatedAtUtc,
            post.MerchantId,
            post.MerchantName);
}
