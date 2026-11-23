using MediatR;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Offers.DTOs;
using Sensus.Domain.Common;
using Sensus.Domain.Entities;
using Sensus.Domain.Enums;
using Sensus.Domain.Repositories;

namespace Sensus.Application.Offers.Commands.ToggleOfferLike;

/// <summary>
/// Alterna el "Me gusta" del usuario autenticado sobre una publicación de producto y devuelve el conteo actualizado.
/// </summary>
public sealed record ToggleOfferLikeCommand(Guid OfferId) : IRequest<Result<OfferLikeStatusDto>>;

public sealed class ToggleOfferLikeCommandHandler : IRequestHandler<ToggleOfferLikeCommand, Result<OfferLikeStatusDto>>
{
    private readonly IOfferRepository _offerRepository;
    private readonly IOfferLikeRepository _offerLikeRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ICacheService? _cacheService;

    public ToggleOfferLikeCommandHandler(
        IOfferRepository offerRepository,
        IOfferLikeRepository offerLikeRepository,
        ICurrentUserService currentUserService,
        IDateTimeProvider dateTimeProvider,
        ICacheService? cacheService = null)
    {
        _offerRepository = offerRepository;
        _offerLikeRepository = offerLikeRepository;
        _currentUserService = currentUserService;
        _dateTimeProvider = dateTimeProvider;
        _cacheService = cacheService;
    }

    public async Task<Result<OfferLikeStatusDto>> Handle(
        ToggleOfferLikeCommand request,
        CancellationToken cancellationToken)
    {
        if (_currentUserService.IsGuest || !Guid.TryParse(_currentUserService.UserId, out Guid userId))
        {
            return Result.Failure<OfferLikeStatusDto>(
                Error.Unauthorized("OfferLike.Unauthorized", "Debes iniciar sesión para dar me gusta."));
        }

        Offer? offer = await _offerRepository.GetByIdAsync(request.OfferId, cancellationToken);
        if (offer is null || offer.Status is OfferStatus.Draft or OfferStatus.Archived)
        {
            return Result.Failure<OfferLikeStatusDto>(
                Error.NotFound("Offer.NotFound", "La publicación solicitada no existe o no está disponible."));
        }

        bool isLiked = await _offerLikeRepository.ToggleLikeAsync(
            userId,
            offer.Id,
            _dateTimeProvider.UtcNow,
            cancellationToken);

        int likesCount = await _offerLikeRepository.CountByOfferIdAsync(offer.Id, cancellationToken);

        if (_cacheService is not null)
        {
            await _cacheService.RemoveAsync(OfferCacheKeys.Detail(offer.Id), cancellationToken);
        }

        return Result.Success(new OfferLikeStatusDto(offer.Id, likesCount, isLiked));
    }
}
