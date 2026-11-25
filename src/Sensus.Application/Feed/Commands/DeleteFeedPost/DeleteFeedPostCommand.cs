using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;
using Sensus.Application.Common.Interfaces;
using Sensus.Domain.Common;
using Sensus.Domain.Entities;
using Sensus.Domain.Repositories;

namespace Sensus.Application.Feed.Commands.DeleteFeedPost;

public sealed record DeleteFeedPostCommand(Guid Id) : IRequest<Result>;

public sealed class DeleteFeedPostCommandValidator : AbstractValidator<DeleteFeedPostCommand>
{
    public DeleteFeedPostCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty()
            .WithMessage("El identificador de la publicación es obligatorio.");
    }
}

public sealed class DeleteFeedPostCommandHandler : IRequestHandler<DeleteFeedPostCommand, Result>
{
    private readonly IFeedPostRepository _postRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IMediaStorage? _mediaStorage;
    private readonly ICacheService? _cacheService;
    private readonly ILogger<DeleteFeedPostCommandHandler> _logger;

    public DeleteFeedPostCommandHandler(
        IFeedPostRepository postRepository,
        ICurrentUserService currentUserService,
        IDateTimeProvider dateTimeProvider,
        ILogger<DeleteFeedPostCommandHandler> logger,
        IMediaStorage? mediaStorage = null,
        ICacheService? cacheService = null)
    {
        _cacheService = cacheService;
        _postRepository = postRepository;
        _currentUserService = currentUserService;
        _dateTimeProvider = dateTimeProvider;
        _logger = logger;
        _mediaStorage = mediaStorage;
    }

    public async Task<Result> Handle(DeleteFeedPostCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_currentUserService.UserId)
            || !Guid.TryParse(_currentUserService.UserId, out Guid userId)
            || userId == Guid.Empty)
        {
            return Result.Failure(
                Error.Unauthorized("FeedPost.Unauthorized", "Debes iniciar sesión para eliminar una publicación."));
        }

        FeedPost? post = await _postRepository.GetByIdAsync(request.Id, cancellationToken);
        if (post is null)
        {
            return Result.Failure(
                Error.NotFound("FeedPost.NotFound", "La publicación no existe."));
        }

        bool isAdmin = _currentUserService.Roles.Contains("Administrador", StringComparer.OrdinalIgnoreCase);

        Result deleteResult = post.Delete(userId, isAdmin, _dateTimeProvider.UtcNow);
        if (deleteResult.IsFailure)
        {
            return deleteResult;
        }

        await _postRepository.UpdateAsync(post, cancellationToken);

        // Spec 026 (CA-026-06): la publicación eliminada sale de items y del total en la siguiente lectura.
        if (_cacheService is not null)
        {
            await _cacheService.RemoveByPrefixAsync(FeedCacheKeys.NearbyPrefix, cancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(post.ImagePublicId))
        {
            if (_mediaStorage is null)
            {
                _logger.LogWarning(
                    "Publicación {PostId} eliminada sin purga de imagen: almacenamiento multimedia no disponible",
                    post.Id);
            }
            else
            {
                bool purged = await _mediaStorage.DeleteImageAsync(post.ImagePublicId, cancellationToken);
                if (!purged)
                {
                    _logger.LogWarning(
                        "No se pudo purgar la imagen {ImagePublicId} de la publicación {PostId}",
                        post.ImagePublicId,
                        post.Id);
                }
            }
        }

        _logger.LogInformation(
            "Publicación {PostId} eliminada por usuario {UserId} (IsAdmin: {IsAdmin})",
            post.Id,
            userId,
            isAdmin);

        return Result.Success();
    }
}
