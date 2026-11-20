using Sensus.Domain.Common;

namespace Sensus.Domain.Entities;

/// <summary>
/// Respuesta de un usuario sobre una reseña/comentario de un comercio.
/// </summary>
public sealed class StoreReviewReply
{
    public Guid Id { get; private set; }
    public Guid AuthorUserId { get; private set; }
    public string AuthorName { get; private set; } = string.Empty;
    public string? AuthorPhotoUrl { get; private set; }
    public string Comment { get; private set; } = string.Empty;
    public DateTime CreatedAtUtc { get; private set; }

    private StoreReviewReply()
    {
    }

    private StoreReviewReply(
        Guid id,
        Guid authorUserId,
        string authorName,
        string? authorPhotoUrl,
        string comment,
        DateTime createdAtUtc)
    {
        Id = id;
        AuthorUserId = authorUserId;
        AuthorName = authorName;
        AuthorPhotoUrl = authorPhotoUrl;
        Comment = comment;
        CreatedAtUtc = createdAtUtc;
    }

    public static Result<StoreReviewReply> Create(
        Guid authorUserId,
        string authorName,
        string? authorPhotoUrl,
        string comment,
        DateTime nowUtc)
    {
        if (authorUserId == Guid.Empty)
        {
            return Result.Failure<StoreReviewReply>(
                Error.Validation("StoreReviewReply.AuthorRequired", "El autor de la respuesta es obligatorio."));
        }

        if (string.IsNullOrWhiteSpace(comment) || comment.Trim().Length > 500)
        {
            return Result.Failure<StoreReviewReply>(
                Error.Validation("StoreReviewReply.CommentInvalid", "La respuesta debe tener entre 1 y 500 caracteres."));
        }

        string safeName = string.IsNullOrWhiteSpace(authorName) ? "Usuario Sensus" : authorName.Trim();
        return Result.Success(new StoreReviewReply(
            Guid.NewGuid(),
            authorUserId,
            safeName,
            authorPhotoUrl,
            comment.Trim(),
            nowUtc.ToUniversalTime()));
    }

    public static StoreReviewReply Rehydrate(
        Guid id,
        Guid authorUserId,
        string authorName,
        string? authorPhotoUrl,
        string comment,
        DateTime createdAtUtc) =>
        new(id, authorUserId, authorName, authorPhotoUrl, comment, createdAtUtc);
}

/// <summary>
/// Reseña y calificación (1 a 5 estrellas) estilo Play Store para un comercio local.
/// Soporta "Me gusta" por usuario y respuestas en hilo.
/// </summary>
public sealed class StoreReview : BaseEntity, IAggregateRoot
{
    public const int MinRating = 1;
    public const int MaxRating = 5;

    private readonly List<Guid> _likedByUserIds = [];
    private readonly List<StoreReviewReply> _replies = [];

    public Guid MerchantId { get; private set; }
    public Guid AuthorUserId { get; private set; }
    public string AuthorName { get; private set; } = string.Empty;
    public string? AuthorPhotoUrl { get; private set; }
    public int Rating { get; private set; }
    public string Comment { get; private set; } = string.Empty;
    public DateTime CreatedAtUtc { get; private set; }

    public IReadOnlyCollection<Guid> LikedByUserIds => _likedByUserIds.AsReadOnly();
    public IReadOnlyCollection<StoreReviewReply> Replies => _replies.AsReadOnly();

    private StoreReview() : base()
    {
    }

    private StoreReview(
        Guid id,
        Guid merchantId,
        Guid authorUserId,
        string authorName,
        string? authorPhotoUrl,
        int rating,
        string comment,
        DateTime createdAtUtc) : base(id)
    {
        MerchantId = merchantId;
        AuthorUserId = authorUserId;
        AuthorName = authorName;
        AuthorPhotoUrl = authorPhotoUrl;
        Rating = rating;
        Comment = comment;
        CreatedAtUtc = createdAtUtc;
    }

    public static Result<StoreReview> Create(
        Guid merchantId,
        Guid authorUserId,
        string authorName,
        string? authorPhotoUrl,
        int rating,
        string comment,
        DateTime nowUtc)
    {
        if (merchantId == Guid.Empty)
        {
            return Result.Failure<StoreReview>(
                Error.Validation("StoreReview.MerchantRequired", "El comercio a calificar es obligatorio."));
        }

        if (authorUserId == Guid.Empty)
        {
            return Result.Failure<StoreReview>(
                Error.Validation("StoreReview.AuthorRequired", "Debes iniciar sesión para calificar."));
        }

        if (rating is < MinRating or > MaxRating)
        {
            return Result.Failure<StoreReview>(
                Error.Validation("StoreReview.RatingInvalid", "La calificación debe estar entre 1 y 5 estrellas."));
        }

        if (string.IsNullOrWhiteSpace(comment) || comment.Trim().Length > 600)
        {
            return Result.Failure<StoreReview>(
                Error.Validation("StoreReview.CommentInvalid", "El comentario debe tener entre 1 y 600 caracteres."));
        }

        string safeName = string.IsNullOrWhiteSpace(authorName) ? "Usuario Sensus" : authorName.Trim();
        return Result.Success(new StoreReview(
            Guid.NewGuid(),
            merchantId,
            authorUserId,
            safeName,
            authorPhotoUrl,
            rating,
            comment.Trim(),
            nowUtc.ToUniversalTime()));
    }

    public bool ToggleLike(Guid userId)
    {
        if (userId == Guid.Empty)
        {
            return false;
        }

        if (_likedByUserIds.Remove(userId))
        {
            return false;
        }

        _likedByUserIds.Add(userId);
        return true;
    }

    public Result<StoreReviewReply> AddReply(
        Guid authorUserId,
        string authorName,
        string? authorPhotoUrl,
        string comment,
        DateTime nowUtc)
    {
        Result<StoreReviewReply> replyResult = StoreReviewReply.Create(
            authorUserId,
            authorName,
            authorPhotoUrl,
            comment,
            nowUtc);

        if (replyResult.IsFailure)
        {
            return replyResult;
        }

        _replies.Add(replyResult.Value);
        return replyResult;
    }

    public static StoreReview Rehydrate(
        Guid id,
        Guid merchantId,
        Guid authorUserId,
        string authorName,
        string? authorPhotoUrl,
        int rating,
        string comment,
        DateTime createdAtUtc,
        IEnumerable<Guid>? likedByUserIds = null,
        IEnumerable<StoreReviewReply>? replies = null)
    {
        var review = new StoreReview(
            id,
            merchantId,
            authorUserId,
            authorName,
            authorPhotoUrl,
            rating,
            comment,
            createdAtUtc);

        if (likedByUserIds is not null)
        {
            review._likedByUserIds.AddRange(likedByUserIds.Where(u => u != Guid.Empty).Distinct());
        }

        if (replies is not null)
        {
            review._replies.AddRange(replies);
        }

        return review;
    }
}
