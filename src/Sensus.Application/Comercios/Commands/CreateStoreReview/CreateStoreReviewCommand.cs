using MediatR;
using Sensus.Application.Comercios.DTOs;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Common.Moderation;
using Sensus.Domain.Common;
using Sensus.Domain.Entities;
using Sensus.Domain.Repositories;

namespace Sensus.Application.Comercios.Commands.CreateStoreReview;

/// <summary>
/// Publica una reseña con calificación de 1 a 5 estrellas y comentario moderado para un comercio.
/// </summary>
public sealed record CreateStoreReviewCommand(
    Guid MerchantId,
    int Rating,
    string Comment) : IRequest<Result<StoreReviewsSummaryDto>>;

public sealed class CreateStoreReviewCommandHandler
    : IRequestHandler<CreateStoreReviewCommand, Result<StoreReviewsSummaryDto>>
{
    private readonly IComercioRepository _comercioRepository;
    private readonly IUserRepository _userRepository;
    private readonly IStoreReviewRepository _storeReviewRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ReviewTextModeration _moderation;
    private readonly ICacheService? _cacheService;

    public CreateStoreReviewCommandHandler(
        IComercioRepository comercioRepository,
        IUserRepository userRepository,
        IStoreReviewRepository storeReviewRepository,
        ICurrentUserService currentUserService,
        IDateTimeProvider dateTimeProvider,
        ReviewTextModeration moderation,
        ICacheService? cacheService = null)
    {
        _comercioRepository = comercioRepository;
        _userRepository = userRepository;
        _storeReviewRepository = storeReviewRepository;
        _currentUserService = currentUserService;
        _dateTimeProvider = dateTimeProvider;
        _moderation = moderation;
        _cacheService = cacheService;
    }

    public async Task<Result<StoreReviewsSummaryDto>> Handle(
        CreateStoreReviewCommand request,
        CancellationToken cancellationToken)
    {
        if (_currentUserService.IsGuest || !Guid.TryParse(_currentUserService.UserId, out Guid userId))
        {
            return Result.Failure<StoreReviewsSummaryDto>(
                Error.Unauthorized("StoreReview.Unauthorized", "Debes iniciar sesión para calificar un comercio."));
        }

        Comercio? comercio = await _comercioRepository.GetByIdAsync(request.MerchantId, cancellationToken);
        if (comercio is null || !comercio.Activo)
        {
            return Result.Failure<StoreReviewsSummaryDto>(
                Error.NotFound("Comercio.NotFound", "El comercio solicitado no existe o no está disponible."));
        }

        User? user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        if (user is null)
        {
            return Result.Failure<StoreReviewsSummaryDto>(
                Error.NotFound("User.NotFound", "Usuario no encontrado."));
        }

        Result<StoreReview> reviewResult = StoreReview.Create(
            comercio.Id,
            user.Id,
            user.NombreCompleto,
            user.FotoPerfilUrl,
            request.Rating,
            request.Comment,
            _dateTimeProvider.UtcNow);

        if (reviewResult.IsFailure)
        {
            return Result.Failure<StoreReviewsSummaryDto>(reviewResult.Error);
        }

        var moderationResult = await _moderation.EnsureApprovedAsync(
            reviewResult.Value.Id, "StoreReview", reviewResult.Value.Comment, cancellationToken);
        if (moderationResult.IsFailure)
        {
            return Result.Failure<StoreReviewsSummaryDto>(moderationResult.Error);
        }

        await _storeReviewRepository.AddAsync(reviewResult.Value, cancellationToken);

        if (_cacheService is not null)
        {
            await _cacheService.RemoveAsync($"sensus:merchants:user:{comercio.UsuarioId}", cancellationToken);
        }

        var (reviews, ratings) = await _storeReviewRepository.GetMerchantPageAsync(comercio.Id, 1, 20, cancellationToken);
        return Result.Success(StoreReviewMappingExtensions.BuildSummary(comercio, reviews, ratings, user.Id, 1, 20));
    }
}
