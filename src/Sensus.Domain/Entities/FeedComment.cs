using Sensus.Domain.Common;

namespace Sensus.Domain.Entities;

/// <summary>
/// Comentario moderado publicado dentro de una publicación del feed comunitario (Spec 025).
/// </summary>
public sealed class FeedComment : BaseEntity
{
    public const int MaxTextLength = 300;
    public const string DefaultAuthorDisplayName = "Usuario";

    public Guid PostId { get; private set; }
    public Guid AuthorUserId { get; private set; }
    public string AuthorDisplayName { get; private set; } = DefaultAuthorDisplayName;
    public string? AuthorPhotoUrl { get; private set; }
    public string Text { get; private set; } = string.Empty;
    public DateTime CreatedAtUtc { get; private set; }

    private FeedComment()
    {
    }

    private FeedComment(
        Guid id,
        Guid postId,
        Guid authorUserId,
        string authorDisplayName,
        string? authorPhotoUrl,
        string text,
        DateTime createdAtUtc) : base(id)
    {
        PostId = postId;
        AuthorUserId = authorUserId;
        AuthorDisplayName = authorDisplayName;
        AuthorPhotoUrl = authorPhotoUrl;
        Text = text;
        CreatedAtUtc = createdAtUtc;
    }

    public static Result<FeedComment> Create(
        Guid postId,
        Guid authorUserId,
        string? authorDisplayName,
        string? text,
        DateTime createdAtUtc,
        Guid? id = null,
        string? authorPhotoUrl = null)
    {
        if (postId == Guid.Empty)
        {
            return Result.Failure<FeedComment>(
                Error.Validation("FeedComment.InvalidPost", "El identificador de la publicación es obligatorio."));
        }

        if (authorUserId == Guid.Empty)
        {
            return Result.Failure<FeedComment>(
                Error.Validation("FeedComment.InvalidAuthor", "El autor del comentario es obligatorio."));
        }

        string normalizedText = (text ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalizedText))
        {
            return Result.Failure<FeedComment>(
                Error.Validation("FeedComment.EmptyText", "El comentario no puede estar vacío."));
        }

        if (normalizedText.Length > MaxTextLength)
        {
            return Result.Failure<FeedComment>(
                Error.Validation("FeedComment.TextTooLong", $"El comentario no debe exceder {MaxTextLength} caracteres."));
        }

        string safeDisplayName = string.IsNullOrWhiteSpace(authorDisplayName)
            ? DefaultAuthorDisplayName
            : authorDisplayName.Trim();

        DateTime utcTimestamp = createdAtUtc.Kind == DateTimeKind.Utc
            ? createdAtUtc
            : DateTime.SpecifyKind(createdAtUtc, DateTimeKind.Utc);

        return Result.Success(new FeedComment(
            id ?? Guid.NewGuid(),
            postId,
            authorUserId,
            safeDisplayName,
            string.IsNullOrWhiteSpace(authorPhotoUrl) ? null : authorPhotoUrl.Trim(),
            normalizedText,
            utcTimestamp));
    }
}
