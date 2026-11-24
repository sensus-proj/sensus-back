namespace Sensus.Application.Common.Interfaces;

public sealed record FeedPostLookupResult(bool Exists, bool IsVisible, Guid? AuthorUserId);

/// <summary>
/// Desacopla la consulta de existencia y visibilidad de publicaciones del feed (Spec 024)
/// para que Spec 025 (Comentarios) funcione de forma autónoma.
/// </summary>
public interface IFeedPostVisibilityLookup
{
    Task<FeedPostLookupResult> GetVisibilityAsync(Guid postId, CancellationToken cancellationToken = default);
}
