using MediatR;
using Sensus.Application.Comercios.DTOs;
using Sensus.Application.Common.Interfaces;
using Sensus.Domain.Common;
using Sensus.Domain.Entities;
using Sensus.Domain.Repositories;

namespace Sensus.Application.Comercios.Commands.ToggleStoreReviewLike;

/// <summary>
/// Alterna el "Me gusta" del usuario autenticado sobre un comentario/reseña de tienda.
/// </summary>
public sealed record ToggleStoreReviewLikeCommand(Guid ReviewId) : IRequest<Result<ReviewLikeStatusDto>>;

public sealed class ToggleStoreReviewLikeCommandHandler
    : IRequestHandler<ToggleStoreReviewLikeCommand, Result<ReviewLikeStatusDto>>
{
    private readonly IStoreReviewRepository _storeReviewRepository;
    private readonly ICurrentUserService _currentUserService;

    public ToggleStoreReviewLikeCommandHandler(
        IStoreReviewRepository storeReviewRepository,
        ICurrentUserService currentUserService)
    {
        _storeReviewRepository = storeReviewRepository;
        _currentUserService = currentUserService;
    }

    public async Task<Result<ReviewLikeStatusDto>> Handle(
        ToggleStoreReviewLikeCommand request,
        CancellationToken cancellationToken)
    {
        if (_currentUserService.IsGuest || !Guid.TryParse(_currentUserService.UserId, out Guid userId))
        {
            return Result.Failure<ReviewLikeStatusDto>(
                Error.Unauthorized("StoreReviewLike.Unauthorized", "Debes iniciar sesión para dar me gusta a un comentario."));
        }

        StoreReview? review = await _storeReviewRepository.GetByIdAsync(request.ReviewId, cancellationToken);
        if (review is null)
        {
            return Result.Failure<ReviewLikeStatusDto>(
                Error.NotFound("StoreReview.NotFound", "La reseña solicitada no existe."));
        }

        var likes = await _storeReviewRepository.ToggleLikeAsync(review.Id, userId, cancellationToken);
        return likes is { } updated
            ? Result.Success(new ReviewLikeStatusDto(review.Id, updated.Count, updated.IsLiked))
            : Result.Failure<ReviewLikeStatusDto>(Error.NotFound("StoreReview.NotFound", "La reseña solicitada no existe."));
    }
}
