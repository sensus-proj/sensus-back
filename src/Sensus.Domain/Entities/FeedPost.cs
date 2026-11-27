using Sensus.Domain.Common;
using Sensus.Domain.Enums;

namespace Sensus.Domain.Entities;

/// <summary>
/// Publicación en el feed social comunitario sujeta a moderación preventiva (Spec 024).
/// </summary>
public sealed class FeedPost : BaseEntity
{
    public const int MaxContentLength = 500;
    public const int MinContentLength = 1;
    public const string DefaultAuthorDisplayName = "Usuario";

    public Guid AuthorUserId { get; private set; }
    public string AuthorDisplayName { get; private set; } = DefaultAuthorDisplayName;
    public string? AuthorPhotoUrl { get; private set; }
    public string Content { get; private set; } = string.Empty;
    public string? ImageUrl { get; private set; }
    public string? ImagePublicId { get; private set; }
    public FeedPostStatus Status { get; private set; }
    public bool IsDeleted { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? UpdatedAtUtc { get; private set; }
    public DateTime? DeletedAtUtc { get; private set; }

    /// <summary>
    /// Ubicación que el autor comparte explícitamente al publicar (Spec 026, decisión 2026-11-22).
    /// Sin ubicación, la publicación queda fuera del feed cercano (FR-026-05).
    /// </summary>
    public double? Latitude { get; private set; }

    public double? Longitude { get; private set; }

    public Guid? MerchantId { get; private set; }

    public string? MerchantName { get; private set; }

    public bool HasLocation => Latitude.HasValue && Longitude.HasValue;

    private FeedPost()
    {
    }

    private FeedPost(
        Guid id,
        Guid authorUserId,
        string authorDisplayName,
        string? authorPhotoUrl,
        string content,
        string? imageUrl,
        string? imagePublicId,
        FeedPostStatus status,
        bool isDeleted,
        DateTime createdAtUtc,
        DateTime? updatedAtUtc,
        DateTime? deletedAtUtc,
        double? latitude = null,
        double? longitude = null,
        Guid? merchantId = null,
        string? merchantName = null) : base(id)
    {
        Latitude = latitude;
        Longitude = longitude;
        MerchantId = merchantId is { } mid && mid != Guid.Empty ? mid : null;
        MerchantName = string.IsNullOrWhiteSpace(merchantName) ? null : merchantName.Trim();
        AuthorUserId = authorUserId;
        AuthorDisplayName = authorDisplayName;
        AuthorPhotoUrl = authorPhotoUrl;
        Content = content;
        ImageUrl = imageUrl;
        ImagePublicId = imagePublicId;
        Status = status;
        IsDeleted = isDeleted;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = updatedAtUtc;
        DeletedAtUtc = deletedAtUtc;
    }

    public static Result<FeedPost> Create(
        Guid authorUserId,
        string? authorDisplayName,
        string? content,
        string? imageUrl,
        DateTime createdAtUtc,
        Guid? id = null,
        string? authorPhotoUrl = null,
        FeedPostStatus status = FeedPostStatus.Published,
        string? imagePublicId = null,
        double? latitude = null,
        double? longitude = null,
        Guid? merchantId = null,
        string? merchantName = null)
    {
        if (authorUserId == Guid.Empty)
        {
            return Result.Failure<FeedPost>(
                Error.Validation("FeedPost.InvalidAuthor", "El autor de la publicación es obligatorio."));
        }

        Result locationResult = ValidateLocation(latitude, longitude);
        if (locationResult.IsFailure)
        {
            return Result.Failure<FeedPost>(locationResult.Error);
        }

        string trimmed = (content ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return Result.Failure<FeedPost>(
                Error.Validation("FeedPost.EmptyContent", "El contenido de la publicación no puede estar vacío."));
        }

        if (trimmed.Length < MinContentLength || trimmed.Length > MaxContentLength)
        {
            return Result.Failure<FeedPost>(
                Error.Validation("FeedPost.ContentLengthOutOfRange", $"El contenido debe tener entre {MinContentLength} y {MaxContentLength} caracteres."));
        }

        string resolvedDisplayName = string.IsNullOrWhiteSpace(authorDisplayName)
            ? DefaultAuthorDisplayName
            : authorDisplayName.Trim();

        var post = new FeedPost(
            id: id ?? Guid.NewGuid(),
            authorUserId: authorUserId,
            authorDisplayName: resolvedDisplayName,
            authorPhotoUrl: string.IsNullOrWhiteSpace(authorPhotoUrl) ? null : authorPhotoUrl.Trim(),
            content: trimmed,
            imageUrl: string.IsNullOrWhiteSpace(imageUrl) ? null : imageUrl.Trim(),
            status: status,
            imagePublicId: string.IsNullOrWhiteSpace(imagePublicId) ? null : imagePublicId.Trim(),
            isDeleted: false,
            createdAtUtc: createdAtUtc,
            updatedAtUtc: null,
            deletedAtUtc: null,
            latitude: latitude,
            longitude: longitude,
            merchantId: merchantId,
            merchantName: merchantName);

        return Result.Success(post);
    }

    /// <summary>
    /// Invariante de ubicación (Spec 026): ambas coordenadas o ninguna, finitas y dentro de rango.
    /// </summary>
    public static Result ValidateLocation(double? latitude, double? longitude)
    {
        if (!latitude.HasValue && !longitude.HasValue)
        {
            return Result.Success();
        }

        if (!latitude.HasValue || !longitude.HasValue)
        {
            return Result.Failure(Error.Validation(
                "FeedPost.IncompleteLocation",
                "La ubicación requiere latitud y longitud."));
        }

        double lat = latitude.Value;
        double lng = longitude.Value;
        if (!double.IsFinite(lat) || !double.IsFinite(lng) || lat < -90 || lat > 90 || lng < -180 || lng > 180)
        {
            return Result.Failure(Error.Validation(
                "FeedPost.InvalidLocation",
                "La latitud debe estar entre -90 y 90 y la longitud entre -180 y 180."));
        }

        return Result.Success();
    }

    public static FeedPost Rehydrate(
        Guid id,
        Guid authorUserId,
        string authorDisplayName,
        string? authorPhotoUrl,
        string content,
        string? imageUrl,
        FeedPostStatus status,
        bool isDeleted,
        DateTime createdAtUtc,
        DateTime? updatedAtUtc,
        DateTime? deletedAtUtc,
        string? imagePublicId = null,
        double? latitude = null,
        double? longitude = null,
        Guid? merchantId = null,
        string? merchantName = null)
    {
        return new FeedPost(
            id,
            authorUserId,
            authorDisplayName,
            authorPhotoUrl,
            content,
            imageUrl,
            imagePublicId,
            status,
            isDeleted,
            createdAtUtc,
            updatedAtUtc,
            deletedAtUtc,
            latitude,
            longitude,
            merchantId,
            merchantName);
    }

    /// <summary>
    /// Anonimiza el autor conservando el contenido ya moderado (Spec 061, FR-061-01).
    /// Devuelve <c>false</c> si el post ya estaba anonimizado (idempotencia, FR-061-06).
    /// </summary>
    public bool Anonymize(DateTime updatedAtUtc)
    {
        if (AuthorUserId == Guid.Empty &&
            AuthorDisplayName == DefaultAuthorDisplayName &&
            AuthorPhotoUrl is null)
        {
            return false;
        }

        AuthorUserId = Guid.Empty;
        AuthorDisplayName = DefaultAuthorDisplayName;
        AuthorPhotoUrl = null;
        UpdatedAtUtc = DateTime.SpecifyKind(updatedAtUtc, DateTimeKind.Utc);
        return true;
    }

    public Result MarkAsPublished(DateTime updatedAtUtc)
    {
        if (IsDeleted)
        {
            return Result.Failure(Error.Conflict("FeedPost.AlreadyDeleted", "No se puede publicar un post eliminado."));
        }

        Status = FeedPostStatus.Published;
        UpdatedAtUtc = updatedAtUtc;
        return Result.Success();
    }

    public Result MarkAsQuarantined(DateTime updatedAtUtc)
    {
        if (IsDeleted)
        {
            return Result.Failure(Error.Conflict("FeedPost.AlreadyDeleted", "No se puede poner en cuarentena un post eliminado."));
        }

        Status = FeedPostStatus.Quarantined;
        UpdatedAtUtc = updatedAtUtc;
        return Result.Success();
    }

    public Result Delete(Guid requestingUserId, bool isAdmin, DateTime deletedAtUtc)
    {
        if (IsDeleted)
        {
            return Result.Success(); // Idempotente
        }

        if (requestingUserId != AuthorUserId && !isAdmin)
        {
            return Result.Failure(
                Error.Forbidden("FeedPost.ForbiddenDelete", "Solo el autor original o un administrador pueden eliminar la publicación."));
        }

        IsDeleted = true;
        Status = FeedPostStatus.Deleted;
        DeletedAtUtc = deletedAtUtc;
        UpdatedAtUtc = deletedAtUtc;
        return Result.Success();
    }
}
