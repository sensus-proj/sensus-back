namespace Sensus.Application.Feed.DTOs;

/// <summary>
/// Estado resultante del me gusta y conteo actualizado (Spec 039, FR-039-02).
/// No expone quién reaccionó.
/// </summary>
public sealed record FeedPostLikeStatusDto(bool Liked, int LikeCount);
