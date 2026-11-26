using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Common.Moderation;
using Sensus.Application.Feed.DTOs;
using Sensus.Domain.Common;
using Sensus.Domain.Entities;
using Sensus.Domain.Enums;
using Sensus.Domain.Repositories;

namespace Sensus.Application.Feed.Commands.CreateFeedPost;

public sealed record CreateFeedPostCommand(
    string Content,
    string? ImageUrl = null,
    byte[]? ImageBytes = null,
    string? ImageContentType = null,
    double? Latitude = null,
    double? Longitude = null,
    Guid? MediaAssetId = null) : IRequest<Result<FeedPostDto>>, ISensitiveRequest;

public sealed class CreateFeedPostCommandValidator : AbstractValidator<CreateFeedPostCommand>
{
    public CreateFeedPostCommandValidator()
    {
        RuleFor(x => x.Content)
            .Must(content => !string.IsNullOrWhiteSpace(content))
            .WithMessage("El contenido de la publicación no puede estar vacío.")
            .Must(content => (content ?? string.Empty).Trim().Length >= FeedPost.MinContentLength)
            .WithMessage($"El contenido debe tener al menos {FeedPost.MinContentLength} caracter.")
            .Must(content => (content ?? string.Empty).Trim().Length <= FeedPost.MaxContentLength)
            .WithMessage($"El contenido no debe exceder {FeedPost.MaxContentLength} caracteres.");

        RuleFor(x => x.ImageUrl)
            .Must(string.IsNullOrWhiteSpace)
            .WithMessage("No se permite enviar imageUrl externa; usa multipart/form-data o mediaAssetId de un activo aprobado.");

        RuleFor(x => x)
            .Must(x => x.ImageBytes is not { Length: > 0 } || !x.MediaAssetId.HasValue)
            .WithName("mediaAssetId")
            .WithMessage("No combines una imagen multipart y mediaAssetId en la misma solicitud.");

        // Spec 026: ubicación opcional compartida por el autor; ambas coordenadas o ninguna.
        RuleFor(x => x)
            .Must(x => FeedPost.ValidateLocation(x.Latitude, x.Longitude).IsSuccess)
            .WithName("location")
            .WithMessage("La ubicación requiere latitud entre -90 y 90 y longitud entre -180 y 180.");
    }
}

public sealed class CreateFeedPostCommandHandler : IRequestHandler<CreateFeedPostCommand, Result<FeedPostDto>>
{
    private readonly IFeedPostRepository _postRepository;
    private readonly IUserRepository _userRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ITextModerationService? _textModerationService;
    private readonly IImageModerationService? _imageModerationService;
    private readonly IMediaStorage? _mediaStorage;
    private readonly ICacheService? _cacheService;
    private readonly IMediaAssetRepository? _mediaAssetRepository;
    private readonly IModerationAuditRecorder? _auditRecorder;
    private readonly ILogger<CreateFeedPostCommandHandler> _logger;

    public CreateFeedPostCommandHandler(
        IFeedPostRepository postRepository,
        IUserRepository userRepository,
        ICurrentUserService currentUserService,
        IDateTimeProvider dateTimeProvider,
        ILogger<CreateFeedPostCommandHandler> logger,
        ITextModerationService? textModerationService = null,
        IImageModerationService? imageModerationService = null,
        IMediaStorage? mediaStorage = null,
        ICacheService? cacheService = null,
        IMediaAssetRepository? mediaAssetRepository = null,
        IModerationAuditRecorder? auditRecorder = null)
    {
        _cacheService = cacheService;
        _mediaAssetRepository = mediaAssetRepository;
        _postRepository = postRepository;
        _userRepository = userRepository;
        _currentUserService = currentUserService;
        _dateTimeProvider = dateTimeProvider;
        _logger = logger;
        _textModerationService = textModerationService;
        _imageModerationService = imageModerationService;
        _mediaStorage = mediaStorage;
        _auditRecorder = auditRecorder;
    }

    public async Task<Result<FeedPostDto>> Handle(
        CreateFeedPostCommand request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_currentUserService.UserId)
            || !Guid.TryParse(_currentUserService.UserId, out Guid userId)
            || userId == Guid.Empty)
        {
            return Result.Failure<FeedPostDto>(
                Error.Unauthorized("FeedPost.Unauthorized", "Debes iniciar sesión para publicar."));
        }

        if (!string.IsNullOrWhiteSpace(request.ImageUrl))
        {
            return Result.Failure<FeedPostDto>(
                Error.Validation(
                    "FeedPost.ExternalImageUrlNotAllowed",
                    "No se permite enviar imageUrl externa; usa multipart/form-data o mediaAssetId de un activo aprobado."));
        }

        string trimmedContent = (request.Content ?? string.Empty).Trim();
        User? author = await _userRepository.GetByIdAsync(userId, cancellationToken);
        string authorDisplayName = !string.IsNullOrWhiteSpace(author?.NombreCompleto)
            ? author.NombreCompleto
            : FeedPost.DefaultAuthorDisplayName;
        Guid postId = Guid.NewGuid();

        // Moderación de texto preventiva (fail-closed, FR-024-02 / FR-024-09).
        ModerationEvaluation textEvaluation = await ContentModerationHelper.EvaluateAsync(
            _textModerationService,
            trimmedContent,
            "feed_post",
            "FeedPost.ModerationRejected",
            "La publicación infringe las normas comunitarias y no pudo publicarse.",
            cancellationToken);

        if (!textEvaluation.IsApproved)
        {
            ModerationAuditEntry textAudit = new(
                postId,
                ModerationTargetType.FeedPost,
                textEvaluation.Verdict,
                textEvaluation.Decision,
                "feed_post_create",
                textEvaluation.FlaggedCategories,
                textEvaluation.CategoryScores);

            if (textEvaluation.Verdict == ModerationVerdict.Quarantined)
            {
                return await QuarantinePostAsync(
                    postId,
                    userId,
                    authorDisplayName,
                    author?.FotoPerfilUrl,
                    trimmedContent,
                    textAudit,
                    request.Latitude,
                    request.Longitude,
                    cancellationToken);
            }

            if (_auditRecorder is not null)
            {
                await _auditRecorder.RecordBestEffortAsync(textAudit, cancellationToken);
            }

            return Result.Failure<FeedPostDto>(
                textEvaluation.RejectionError
                ?? Error.Validation("FeedPost.ModerationRejected", "La publicación infringe las normas comunitarias y no pudo publicarse."));
        }

        string? resolvedImageUrl = null;
        string? imagePublicId = null;
        IReadOnlyDictionary<string, double>? publishCategoryScores = textEvaluation.CategoryScores;

        // Moderación de imagen preventiva en la misma solicitud que el texto (fail-closed, FR-024-02 / FR-024-09).
        if (request.ImageBytes is { Length: > 0 })
        {
            (bool IsApproved, string Decision, string? Message, IReadOnlyList<string> FlaggedCategories, IReadOnlyDictionary<string, double>? CategoryScores) imageEvaluation =
                await EvaluateImageModerationAsync(
                    request.ImageBytes,
                    request.ImageContentType ?? "image/jpeg",
                    trimmedContent,
                    cancellationToken);

            if (!imageEvaluation.IsApproved)
            {
                ModerationVerdict imageVerdict = ContentModerationHelper.ToVerdict(imageEvaluation.Decision);
                ModerationAuditEntry imageAudit = new(
                    postId,
                    ModerationTargetType.FeedPost,
                    imageVerdict,
                    imageEvaluation.Decision,
                    "feed_post_create",
                    imageEvaluation.FlaggedCategories,
                    imageEvaluation.CategoryScores);

                // Nunca subimos bytes sin aprobar ni en cuarentena a Cloudinary.
                if (imageVerdict == ModerationVerdict.Quarantined)
                {
                    return await QuarantinePostAsync(
                        postId,
                        userId,
                        authorDisplayName,
                        author?.FotoPerfilUrl,
                        trimmedContent,
                        imageAudit,
                        request.Latitude,
                        request.Longitude,
                        cancellationToken);
                }

                if (_auditRecorder is not null)
                {
                    await _auditRecorder.RecordBestEffortAsync(imageAudit, cancellationToken);
                }

                return Result.Failure<FeedPostDto>(
                    Error.Validation(
                        "FeedPost.ImageModerationRejected",
                        imageEvaluation.Message ?? "La imagen adjunta infringe las normas de moderación y no pudo publicarse."));
            }

            if (imageEvaluation.CategoryScores is not null)
            {
                publishCategoryScores = imageEvaluation.CategoryScores;
            }

            if (_mediaStorage is null)
            {
                return Result.Failure<FeedPostDto>(
                    Error.Failure("FeedPost.MediaStorageUnavailable", "El almacenamiento multimedia no está disponible; la publicación no fue creada."));
            }
        }
        else if (request.MediaAssetId is Guid mediaAssetId)
        {
            if (mediaAssetId == Guid.Empty || _mediaAssetRepository is null)
            {
                return Result.Failure<FeedPostDto>(
                    Error.Validation(
                        "FeedPost.MediaNotApproved",
                        "El activo multimedia no existe, no está aprobado o no pertenece al autor."));
            }

            MediaAsset? asset = await _mediaAssetRepository.GetByIdAsync(mediaAssetId, cancellationToken);
            if (asset is null
                || asset.OwnerId != userId
                || asset.Status != MediaAssetStatus.Approved
                || string.IsNullOrWhiteSpace(asset.SecureUrl))
            {
                return Result.Failure<FeedPostDto>(
                    Error.Validation(
                        "FeedPost.MediaNotApproved",
                        "El activo multimedia no existe, no está aprobado o no pertenece al autor."));
            }

            resolvedImageUrl = asset.SecureUrl;
            imagePublicId = asset.PublicId;
        }

        // Spec 021 (FR-021-01): sin evidencia persistida la publicación no se publica (fail-closed).
        if (_auditRecorder is null)
        {
            return Result.Failure<FeedPostDto>(
                Error.Unavailable("ModerationAudit.Unavailable", "No se pudo registrar la auditoría de moderación."));
        }

        ModerationAuditEntry publishAudit = new(
            postId,
            ModerationTargetType.FeedPost,
            ModerationVerdict.Approved,
            ContentModerationHelper.ApprovedDecision,
            "feed_post_create",
            [],
            publishCategoryScores);

        Result auditResult = await _auditRecorder.RecordAsync(publishAudit, cancellationToken);
        if (auditResult.IsFailure)
        {
            return Result.Failure<FeedPostDto>(auditResult.Error);
        }

        if (request.ImageBytes is { Length: > 0 })
        {
            Result<MediaUploadResult> uploadResult = await _mediaStorage!.UploadImageAsync(
                request.ImageBytes,
                request.ImageContentType ?? "image/jpeg",
                userId,
                cancellationToken);

            if (uploadResult.IsFailure)
            {
                return Result.Failure<FeedPostDto>(uploadResult.Error);
            }

            resolvedImageUrl = uploadResult.Value.SecureUrl;
            imagePublicId = uploadResult.Value.PublicId;
        }

        Result<FeedPost> entityResult = FeedPost.Create(
            authorUserId: userId,
            authorDisplayName: authorDisplayName,
            content: trimmedContent,
            imageUrl: resolvedImageUrl,
            createdAtUtc: _dateTimeProvider.UtcNow,
            authorPhotoUrl: author?.FotoPerfilUrl,
            id: postId,
            status: FeedPostStatus.Published,
            imagePublicId: imagePublicId,
            latitude: request.Latitude,
            longitude: request.Longitude);

        if (entityResult.IsFailure)
        {
            return Result.Failure<FeedPostDto>(entityResult.Error);
        }

        FeedPost post = entityResult.Value;
        await _postRepository.AddAsync(post, cancellationToken);

        // Spec 026 (FR-026-06): la nueva publicación aparece en la siguiente lectura del feed cercano.
        if (_cacheService is not null && post.HasLocation)
        {
            await _cacheService.RemoveByPrefixAsync(FeedCacheKeys.NearbyPrefix, cancellationToken);
        }

        _logger.LogInformation("Publicación {PostId} creada por usuario {UserId}", post.Id, userId);

        return Result.Success(post.ToDto());
    }

    private async Task<(bool IsApproved, string Decision, string? Message, IReadOnlyList<string> FlaggedCategories, IReadOnlyDictionary<string, double>? CategoryScores)> EvaluateImageModerationAsync(
        byte[] imageBytes,
        string contentType,
        string caption,
        CancellationToken cancellationToken)
    {
        if (_imageModerationService is null)
        {
            return (false, ContentModerationHelper.QuarantineDecision, "La moderación de imágenes no está disponible; la publicación quedó en cuarentena.", [], null);
        }

        try
        {
            ImageModerationResult? result = await _imageModerationService.ModerateImageBytesAsync(
                imageBytes,
                contentType,
                caption,
                cancellationToken);

            if (result is null)
            {
                return (false, ContentModerationHelper.QuarantineDecision, "La moderación de imágenes devolvió una respuesta nula; la publicación quedó en cuarentena.", [], null);
            }

            if (result.IsApproved)
            {
                string approvedDecision = string.IsNullOrWhiteSpace(result.Decision)
                    ? ContentModerationHelper.ApprovedDecision
                    : result.Decision;
                return (true, approvedDecision, result.Message, result.FlaggedCategories ?? [], result.CategoryScores);
            }

            string decision = string.IsNullOrWhiteSpace(result.Decision)
                ? ContentModerationHelper.QuarantineDecision
                : result.Decision;
            return (false, decision, result.Message, result.FlaggedCategories ?? [], result.CategoryScores);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return (false, ContentModerationHelper.QuarantineDecision, "La moderación de imágenes falló; la publicación quedó en cuarentena.", [], null);
        }
    }

    private async Task<Result<FeedPostDto>> QuarantinePostAsync(
        Guid postId,
        Guid userId,
        string authorDisplayName,
        string? authorPhotoUrl,
        string content,
        ModerationAuditEntry auditEntry,
        double? latitude,
        double? longitude,
        CancellationToken cancellationToken)
    {
        if (_auditRecorder is not null)
        {
            await _auditRecorder.RecordBestEffortAsync(auditEntry, cancellationToken);
        }

        Result<FeedPost> entityResult = FeedPost.Create(
            authorUserId: userId,
            authorDisplayName: authorDisplayName,
            content: content,
            imageUrl: null,
            createdAtUtc: _dateTimeProvider.UtcNow,
            authorPhotoUrl: authorPhotoUrl,
            id: postId,
            status: FeedPostStatus.Quarantined,
            imagePublicId: null,
            latitude: latitude,
            longitude: longitude);

        if (entityResult.IsFailure)
        {
            return Result.Failure<FeedPostDto>(entityResult.Error);
        }

        FeedPost post = entityResult.Value;
        await _postRepository.AddAsync(post, cancellationToken);

        _logger.LogWarning(
            "Publicación {PostId} de usuario {UserId} enviada a cuarentena por indisponibilidad del moderador.",
            post.Id,
            userId);

        return Result.Success(post.ToDto());
    }
}
