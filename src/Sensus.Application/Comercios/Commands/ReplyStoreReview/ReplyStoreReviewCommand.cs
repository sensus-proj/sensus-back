using MediatR;
using Sensus.Application.Comercios.DTOs;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Common.Moderation;
using Sensus.Domain.Common;
using Sensus.Domain.Entities;
using Sensus.Domain.Repositories;

namespace Sensus.Application.Comercios.Commands.ReplyStoreReview;

/// <summary>
/// Agrega una respuesta moderada a una reseña existente de un comercio.
/// </summary>
public sealed record ReplyStoreReviewCommand(Guid ReviewId, string Comment) : IRequest<Result<StoreReviewDto>>;

public sealed class ReplyStoreReviewCommandHandler
    : IRequestHandler<ReplyStoreReviewCommand, Result<StoreReviewDto>>
{
    private readonly IStoreReviewRepository _storeReviewRepository;
    private readonly IUserRepository _userRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ReviewTextModeration _moderation;
    private readonly IComercioRepository _merchants;

    public ReplyStoreReviewCommandHandler(
        IStoreReviewRepository storeReviewRepository,
        IUserRepository userRepository,
        ICurrentUserService currentUserService,
        IDateTimeProvider dateTimeProvider,
        ReviewTextModeration moderation,
        IComercioRepository merchants)
    {
        _storeReviewRepository = storeReviewRepository;
        _userRepository = userRepository;
        _currentUserService = currentUserService;
        _dateTimeProvider = dateTimeProvider;
        _moderation = moderation;
        _merchants = merchants;
    }

    public async Task<Result<StoreReviewDto>> Handle(
        ReplyStoreReviewCommand request,
        CancellationToken cancellationToken)
    {
        if (_currentUserService.IsGuest || !Guid.TryParse(_currentUserService.UserId, out Guid userId))
        {
            return Result.Failure<StoreReviewDto>(
                Error.Unauthorized("StoreReviewReply.Unauthorized", "Debes iniciar sesión para responder un comentario."));
        }

        StoreReview? review = await _storeReviewRepository.GetByIdAsync(request.ReviewId, cancellationToken);
        if (review is null || await _merchants.GetByIdAsync(review.MerchantId, cancellationToken) is not { Activo: true })
        {
            return Result.Failure<StoreReviewDto>(
                Error.NotFound("StoreReview.NotFound", "La reseña solicitada no existe."));
        }

        User? user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        if (user is null)
        {
            return Result.Failure<StoreReviewDto>(
                Error.NotFound("User.NotFound", "Usuario no encontrado."));
        }

        Result<StoreReviewReply> replyResult = review.AddReply(
            user.Id,
            user.NombreCompleto,
            user.FotoPerfilUrl,
            request.Comment,
            _dateTimeProvider.UtcNow);

        if (replyResult.IsFailure)
        {
            return Result.Failure<StoreReviewDto>(replyResult.Error);
        }

        var moderationResult = await _moderation.EnsureApprovedAsync(
            replyResult.Value.Id, "ReviewReply", replyResult.Value.Comment, cancellationToken);
        if (moderationResult.IsFailure)
        {
            return Result.Failure<StoreReviewDto>(moderationResult.Error);
        }

        if (!await _storeReviewRepository.AddReplyAsync(review.Id, replyResult.Value, cancellationToken))
        {
            return Result.Failure<StoreReviewDto>(Error.NotFound("StoreReview.NotFound", "La reseña solicitada no existe."));
        }

        return Result.Success(review.ToDto(user.Id));
    }
}
