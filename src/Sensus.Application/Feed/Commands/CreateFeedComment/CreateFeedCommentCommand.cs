using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Common.Moderation;
using Sensus.Application.Feed.DTOs;
using Sensus.Domain.Common;
using Sensus.Domain.Entities;
using Sensus.Domain.Repositories;

namespace Sensus.Application.Feed.Commands.CreateFeedComment;

public sealed record CreateFeedCommentCommand(Guid PostId, string Text) : IRequest<Result<FeedCommentDto>>;

public sealed class CreateFeedCommentCommandValidator : AbstractValidator<CreateFeedCommentCommand>
{
    public CreateFeedCommentCommandValidator()
    {
        RuleFor(x => x.PostId)
            .NotEmpty()
            .WithMessage("El identificador de la publicación es obligatorio.");

        RuleFor(x => x.Text)
            .Must(text => !string.IsNullOrWhiteSpace(text))
            .WithMessage("El comentario no puede estar vacío.")
            .Must(text => (text ?? string.Empty).Trim().Length <= FeedComment.MaxTextLength)
            .WithMessage($"El comentario no debe exceder {FeedComment.MaxTextLength} caracteres.");
    }
}

public sealed class CreateFeedCommentCommandHandler : IRequestHandler<CreateFeedCommentCommand, Result<FeedCommentDto>>
{
    private readonly IFeedCommentRepository _commentRepository;
    private readonly IFeedPostVisibilityLookup _postLookup;
    private readonly IUserRepository _userRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ILogger<CreateFeedCommentCommandHandler> _logger;
    private readonly ITextModerationService? _textModerationService;

    public CreateFeedCommentCommandHandler(
        IFeedCommentRepository commentRepository,
        IFeedPostVisibilityLookup postLookup,
        IUserRepository userRepository,
        ICurrentUserService currentUserService,
        IDateTimeProvider dateTimeProvider,
        ILogger<CreateFeedCommentCommandHandler> logger,
        ITextModerationService? textModerationService = null)
    {
        _commentRepository = commentRepository;
        _postLookup = postLookup;
        _userRepository = userRepository;
        _currentUserService = currentUserService;
        _dateTimeProvider = dateTimeProvider;
        _logger = logger;
        _textModerationService = textModerationService;
    }

    public async Task<Result<FeedCommentDto>> Handle(CreateFeedCommentCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_currentUserService.UserId)
            || !Guid.TryParse(_currentUserService.UserId, out Guid userId)
            || userId == Guid.Empty)
        {
            return Result.Failure<FeedCommentDto>(
                Error.Unauthorized("FeedComment.Unauthorized", "Debes iniciar sesión para comentar."));
        }

        FeedPostLookupResult postLookup = await _postLookup.GetVisibilityAsync(request.PostId, cancellationToken);
        if (!postLookup.Exists || !postLookup.IsVisible)
        {
            return Result.Failure<FeedCommentDto>(
                Error.NotFound("FeedComment.PostNotFound", "La publicación no existe o no está disponible."));
        }

        string trimmedText = (request.Text ?? string.Empty).Trim();
        Result moderationResult = await ContentModerationHelper.EnsureApprovedAsync(
            _textModerationService,
            trimmedText,
            "feed_comment",
            "FeedComment.ModerationRejected",
            "El comentario infringe las normas comunitarias y no pudo publicarse.",
            cancellationToken);

        if (moderationResult.IsFailure)
        {
            return Result.Failure<FeedCommentDto>(moderationResult.Error);
        }

        User? author = await _userRepository.GetByIdAsync(userId, cancellationToken);
        string authorDisplayName = !string.IsNullOrWhiteSpace(author?.NombreCompleto)
            ? author.NombreCompleto
            : FeedComment.DefaultAuthorDisplayName;

        Result<FeedComment> entityResult = FeedComment.Create(
            request.PostId,
            userId,
            authorDisplayName,
            trimmedText,
            _dateTimeProvider.UtcNow,
            authorPhotoUrl: author?.FotoPerfilUrl);

        if (entityResult.IsFailure)
        {
            return Result.Failure<FeedCommentDto>(entityResult.Error);
        }

        FeedComment comment = entityResult.Value;
        await _commentRepository.AddAsync(comment, cancellationToken);

        _logger.LogInformation(
            "Comentario {CommentId} publicado en publicación {PostId}.",
            comment.Id,
            comment.PostId);

        return Result.Success(comment.ToDto());
    }
}
