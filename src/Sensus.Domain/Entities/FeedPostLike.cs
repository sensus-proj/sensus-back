using Sensus.Domain.Common;

namespace Sensus.Domain.Entities;

/// <summary>
/// Me gusta de un usuario sobre una publicación del feed (Spec 039).
/// La pareja publicación + usuario es la identidad natural del registro: solo puede existir uno (FR-039-04).
/// </summary>
public sealed class FeedPostLike : BaseEntity
{
    public Guid PostId { get; private set; }
    public Guid UserId { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    private FeedPostLike()
    {
    }

    private FeedPostLike(Guid id, Guid postId, Guid userId, DateTime createdAtUtc) : base(id)
    {
        PostId = postId;
        UserId = userId;
        CreatedAtUtc = createdAtUtc;
    }

    public static Result<FeedPostLike> Create(Guid postId, Guid userId, DateTime nowUtc)
    {
        if (postId == Guid.Empty)
        {
            return Result.Failure<FeedPostLike>(
                Error.Validation("FeedPostLike.PostRequired", "La publicación es obligatoria para dar me gusta."));
        }

        if (userId == Guid.Empty)
        {
            return Result.Failure<FeedPostLike>(
                Error.Validation("FeedPostLike.UserRequired", "El usuario es obligatorio para dar me gusta."));
        }

        return Result.Success(new FeedPostLike(
            Guid.NewGuid(),
            postId,
            userId,
            DateTime.SpecifyKind(nowUtc, DateTimeKind.Utc)));
    }

    public static FeedPostLike Rehydrate(Guid id, Guid postId, Guid userId, DateTime createdAtUtc) =>
        new(id, postId, userId, createdAtUtc);

    /// <summary>
    /// Conteo resultante tras una transición, acotado a cero (NFR-039-02: cero conteos negativos).
    /// </summary>
    public static int ClampCount(long count) =>
        count <= 0 ? 0 : (int)Math.Min(count, int.MaxValue);
}
