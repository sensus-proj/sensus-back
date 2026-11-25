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
    string? ImageContentType = null) : IRequest<Result<FeedPostDto>>;

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
            .Must(url => string.IsNullOrWhiteSpace(url) || Uri.TryCreate(url, UriKind.Absolute, out _))
            .WithMessage("La URL de la imagen debe ser una URL absoluta válida.");
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
    private readonly ILogger<CreateFeedPostCommandHandler> _logger;

    public CreateFeedPostCommandHandler(
        IFeedPostRepository postRepository,
        IUserRepository userRepository,
        ICurrentUserService currentUserService,
        IDateTimeProvider dateTimeProvider,
        ILogger<CreateFeedPostCommandHandler> logger,
        ITextModerationService? textModerationService = null,
        IImageModerationService? imageModerationService = null,
        IMediaStorage? mediaStorage = null)
    {
        _postRepository = postRepository;
        _userRepository = userRepository;
        _currentUserService = currentUserService;
        _dateTimeProvider = dateTimeProvider;
        _logger = logger;
        _textModerationService = textModerationService;
        _imageModerationService = imageModerationService;
        _mediaStorage = mediaStorage;
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

        if (_textModerationService is null)
        {
            return Result.Failure<FeedPostDto>(
                Error.Failure("FeedPost.ModerationUnavailable", "La moderación no está disponible; la publicación no fue creada."));
        }

        string trimmedContent = (request.Content ?? string.Empty).Trim();

        // Moderación de texto preventiva (fail-closed, FR-024-02)
        Result textModerationResult = await ContentModerationHelper.EnsureApprovedAsync(
            _textModerationService,
            trimmedContent,
            "feed_post",
            "FeedPost.ModerationRejected",
            "La publicación infringe las normas comunitarias y no pudo publicarse.",
            cancellationToken);

        if (textModerationResult.IsFailure)
        {
            return Result.Failure<FeedPostDto>(textModerationResult.Error);
        }

        string? resolvedImageUrl = request.ImageUrl;
        string? imagePublicId = null;

        // Moderación de imagen preventiva en la misma solicitud que el texto (fail-closed, FR-024-02).
        if (request.ImageBytes is { Length: > 0 })
        {
            if (_imageModerationService is null)
            {
                return Result.Failure<FeedPostDto>(
                    Error.Failure("FeedPost.ImageModerationUnavailable", "La moderación de imágenes no está disponible; la publicación no fue creada."));
            }

            ImageModerationResult imageResult = await _imageModerationService.ModerateImageBytesAsync(
                request.ImageBytes,
                request.ImageContentType ?? "image/jpeg",
                trimmedContent,
                cancellationToken);

            if (!imageResult.IsApproved)
            {
                return Result.Failure<FeedPostDto>(
                    Error.Validation("FeedPost.ImageModerationRejected",
                        imageResult.Message ?? "La imagen adjunta infringe las normas de moderación y no pudo publicarse."));
            }

            if (_mediaStorage is null)
            {
                return Result.Failure<FeedPostDto>(
                    Error.Failure("FeedPost.MediaStorageUnavailable", "El almacenamiento multimedia no está disponible; la publicación no fue creada."));
            }

            Result<MediaUploadResult> uploadResult = await _mediaStorage.UploadImageAsync(
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

        User? author = await _userRepository.GetByIdAsync(userId, cancellationToken);
        string authorDisplayName = !string.IsNullOrWhiteSpace(author?.NombreCompleto)
            ? author.NombreCompleto
            : FeedPost.DefaultAuthorDisplayName;

        Result<FeedPost> entityResult = FeedPost.Create(
            authorUserId: userId,
            authorDisplayName: authorDisplayName,
            content: trimmedContent,
            imageUrl: resolvedImageUrl,
            createdAtUtc: _dateTimeProvider.UtcNow,
            authorPhotoUrl: author?.FotoPerfilUrl,
            status: FeedPostStatus.Published,
            imagePublicId: imagePublicId);

        if (entityResult.IsFailure)
        {
            return Result.Failure<FeedPostDto>(entityResult.Error);
        }

        FeedPost post = entityResult.Value;
        await _postRepository.AddAsync(post, cancellationToken);

        _logger.LogInformation("Publicación {PostId} creada por usuario {UserId}", post.Id, userId);

        return Result.Success(post.ToDto());
    }
}
