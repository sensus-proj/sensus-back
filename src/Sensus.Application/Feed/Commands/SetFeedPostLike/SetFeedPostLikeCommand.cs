using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Feed.DTOs;
using Sensus.Domain.Common;
using Sensus.Domain.Entities;
using Sensus.Domain.Repositories;

namespace Sensus.Application.Feed.Commands.SetFeedPostLike;

/// <summary>
/// Establece el me gusta del usuario autenticado sobre una publicación visible (Spec 039, FR-039-01).
/// El cliente envía la intención explícita <paramref name="Liked"/>; dos solicitudes con la misma
/// intención producen una sola transición (FR-039-05, CA-039-04).
/// </summary>
public sealed record SetFeedPostLikeCommand(Guid PostId, bool Liked) : IRequest<Result<FeedPostLikeStatusDto>>;

public sealed class SetFeedPostLikeCommandValidator : AbstractValidator<SetFeedPostLikeCommand>
{
    public SetFeedPostLikeCommandValidator()
    {
        RuleFor(x => x.PostId)
            .NotEmpty()
            .WithMessage("El identificador de la publicación es obligatorio.");
    }
}

public sealed class SetFeedPostLikeCommandHandler : IRequestHandler<SetFeedPostLikeCommand, Result<FeedPostLikeStatusDto>>
{
    private readonly IFeedPostVisibilityLookup _postLookup;
    private readonly IFeedPostLikeRepository _likeRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ILogger<SetFeedPostLikeCommandHandler> _logger;

    public SetFeedPostLikeCommandHandler(
        IFeedPostVisibilityLookup postLookup,
        IFeedPostLikeRepository likeRepository,
        ICurrentUserService currentUserService,
        IDateTimeProvider dateTimeProvider,
        ILogger<SetFeedPostLikeCommandHandler> logger)
    {
        _postLookup = postLookup;
        _likeRepository = likeRepository;
        _currentUserService = currentUserService;
        _dateTimeProvider = dateTimeProvider;
        _logger = logger;
    }

    public async Task<Result<FeedPostLikeStatusDto>> Handle(
        SetFeedPostLikeCommand request,
        CancellationToken cancellationToken)
    {
        // El actor sale exclusivamente de la sesión (STRIDE: suplantación).
        if (_currentUserService.IsGuest
            || !Guid.TryParse(_currentUserService.UserId, out Guid userId)
            || userId == Guid.Empty)
        {
            return Result.Failure<FeedPostLikeStatusDto>(
                Error.Unauthorized("FeedPostLike.Unauthorized", "Debes iniciar sesión para dar me gusta."));
        }

        // FR-039-03: 404 uniforme para publicaciones inexistentes, en cuarentena, pendientes o eliminadas.
        FeedPostLookupResult lookup = await _postLookup.GetVisibilityAsync(request.PostId, cancellationToken);
        if (!lookup.Exists || !lookup.IsVisible)
        {
            return Result.Failure<FeedPostLikeStatusDto>(
                Error.NotFound("FeedPost.NotFound", "La publicación no existe o no está disponible."));
        }

        bool changed;
        if (request.Liked)
        {
            Result<FeedPostLike> like = FeedPostLike.Create(request.PostId, userId, _dateTimeProvider.UtcNow);
            if (like.IsFailure)
            {
                return Result.Failure<FeedPostLikeStatusDto>(like.Error);
            }

            changed = await _likeRepository.AddAsync(like.Value, cancellationToken);
        }
        else
        {
            changed = await _likeRepository.RemoveAsync(request.PostId, userId, cancellationToken);
        }

        int likeCount = FeedPostLike.ClampCount(
            await _likeRepository.CountByPostIdAsync(request.PostId, cancellationToken));

        _logger.LogInformation(
            "Me gusta de publicación {PostId}: intención {Liked}, transición aplicada {Changed}",
            request.PostId,
            request.Liked,
            changed);

        return Result.Success(new FeedPostLikeStatusDto(request.Liked, likeCount));
    }
}
