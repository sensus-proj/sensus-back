using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Offers;
using Sensus.Application.Offers.DTOs;
using Sensus.Domain.Common;
using Sensus.Domain.Entities;
using Sensus.Domain.Enums;
using Sensus.Domain.Repositories;

namespace Sensus.Application.Offers.Commands.ToggleOfferFavorite;

/// <summary>
/// Alterna el favorito del usuario autenticado sobre una oferta activa (Spec 038).
/// </summary>
public sealed record ToggleOfferFavoriteCommand(Guid OfferId) : IRequest<Result<FavoriteToggleResultDto>>;

public sealed class ToggleOfferFavoriteCommandValidator : AbstractValidator<ToggleOfferFavoriteCommand>
{
    public ToggleOfferFavoriteCommandValidator()
    {
        RuleFor(x => x.OfferId)
            .NotEmpty()
            .WithMessage("El identificador de la oferta es obligatorio.");
    }
}

public sealed class ToggleOfferFavoriteCommandHandler : IRequestHandler<ToggleOfferFavoriteCommand, Result<FavoriteToggleResultDto>>
{
    private readonly IOfferRepository _offerRepository;
    private readonly IOfferFavoriteRepository _favoriteRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ICacheService? _cacheService;
    private readonly ILogger<ToggleOfferFavoriteCommandHandler> _logger;

    public ToggleOfferFavoriteCommandHandler(
        IOfferRepository offerRepository,
        IOfferFavoriteRepository favoriteRepository,
        ICurrentUserService currentUserService,
        IDateTimeProvider dateTimeProvider,
        ILogger<ToggleOfferFavoriteCommandHandler> logger,
        ICacheService? cacheService = null)
    {
        _offerRepository = offerRepository;
        _favoriteRepository = favoriteRepository;
        _currentUserService = currentUserService;
        _dateTimeProvider = dateTimeProvider;
        _logger = logger;
        _cacheService = cacheService;
    }

    public async Task<Result<FavoriteToggleResultDto>> Handle(
        ToggleOfferFavoriteCommand request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_currentUserService.UserId)
            || !Guid.TryParse(_currentUserService.UserId, out Guid userId)
            || userId == Guid.Empty)
        {
            return Result.Failure<FavoriteToggleResultDto>(
                Error.Unauthorized("Favorite.Unauthorized", "Debes iniciar sesión para agregar a favoritos."));
        }

        if (_currentUserService.IsGuest)
        {
            return Result.Failure<FavoriteToggleResultDto>(
                Error.Forbidden("Favorite.GuestForbidden", "Los invitados no pueden agregar ofertas a favoritos."));
        }

        Offer? offer = await _offerRepository.GetByIdAsync(request.OfferId, cancellationToken);
        DateTime nowUtc = _dateTimeProvider.UtcNow;

        if (offer is null
            || offer.Status is OfferStatus.Draft or OfferStatus.Archived or OfferStatus.Expired or OfferStatus.PendingModeration or OfferStatus.Rejected
            || offer.EndsAt <= nowUtc)
        {
            return Result.Failure<FavoriteToggleResultDto>(
                Error.NotFound("Offer.NotFound", "La oferta no existe o no está disponible."));
        }

        bool favorited = await _favoriteRepository.ToggleFavoriteAsync(
            userId,
            offer.Id,
            nowUtc,
            cancellationToken);

        int count = await _favoriteRepository.CountByOfferIdAsync(offer.Id, cancellationToken);

        if (_cacheService is not null)
        {
            await _cacheService.RemoveAsync(OfferCacheKeys.Detail(offer.Id), cancellationToken);
            // FR-038-05 / FR-042-04: los favoritos cambian la afinidad por categoría del feed.
            await _cacheService.RemoveAsync(FeedCacheKeys.Affinity(userId), cancellationToken);
        }

        _logger.LogInformation(
            "Usuario {UserId} alternó favorito en oferta {OfferId}. Estado: {Favorited}, Total: {Count}",
            userId,
            offer.Id,
            favorited,
            count);

        return Result.Success(new FavoriteToggleResultDto(offer.Id, favorited, count));
    }
}
