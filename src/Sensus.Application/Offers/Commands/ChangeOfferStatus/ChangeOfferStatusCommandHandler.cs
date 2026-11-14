using MediatR;
using Microsoft.Extensions.Logging;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Offers.DTOs;
using Sensus.Domain.Common;
using Sensus.Domain.Entities;
using Sensus.Domain.Enums;
using Sensus.Domain.Repositories;

namespace Sensus.Application.Offers.Commands.ChangeOfferStatus;

public sealed class ChangeOfferStatusCommandHandler : IRequestHandler<ChangeOfferStatusCommand, Result<OfferDto>>
{
    private readonly IOfferRepository _offerRepository;
    private readonly IOfferClaimRepository _offerClaimRepository;
    private readonly IComercioRepository _comercioRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ILogger<ChangeOfferStatusCommandHandler> _logger;

    public ChangeOfferStatusCommandHandler(
        IOfferRepository offerRepository,
        IOfferClaimRepository offerClaimRepository,
        IComercioRepository comercioRepository,
        ICurrentUserService currentUserService,
        IDateTimeProvider dateTimeProvider,
        ILogger<ChangeOfferStatusCommandHandler> logger)
    {
        _offerRepository = offerRepository;
        _offerClaimRepository = offerClaimRepository;
        _comercioRepository = comercioRepository;
        _currentUserService = currentUserService;
        _dateTimeProvider = dateTimeProvider;
        _logger = logger;
    }

    public async Task<Result<OfferDto>> Handle(ChangeOfferStatusCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_currentUserService.UserId) || !Guid.TryParse(_currentUserService.UserId, out var userId))
        {
            return Result.Failure<OfferDto>(Error.Unauthorized("Offer.Unauthorized", "Debe iniciar sesión para modificar el estado de la oferta."));
        }

        Comercio? comercio = await _comercioRepository.GetByUsuarioIdAsync(userId, cancellationToken);
        if (comercio is null || !comercio.Activo)
        {
            return Result.Failure<OfferDto>(Error.Forbidden("Offer.NoActiveMerchant", "El usuario no tiene un comercio activo registrado."));
        }

        Offer? offer = await _offerRepository.GetByIdAsync(request.OfferId, cancellationToken);
        if (offer is null)
        {
            return Result.Failure<OfferDto>(Error.NotFound("Offer.NotFound", "La oferta especificada no existe."));
        }

        if (offer.MerchantId != comercio.Id)
        {
            return Result.Failure<OfferDto>(Error.Forbidden("Offer.Forbidden", "No tiene autorización para modificar una oferta de otro comercio."));
        }

        if (!Enum.TryParse<OfferStatus>(request.NewStatus, true, out var targetStatus))
        {
            return Result.Failure<OfferDto>(Error.Validation("Offer.InvalidStatus", $"El estado '{request.NewStatus}' no es válido."));
        }

        DateTime now = _dateTimeProvider.UtcNow;
        OfferStatus previousStatus = offer.Status;

        if (previousStatus == OfferStatus.Archived)
        {
            return Result.Failure<OfferDto>(Error.Conflict("Offer.CannotTransitionFromArchived", "Una oferta archivada no puede cambiar de estado mediante este endpoint."));
        }

        Result transitionResult;
        switch (targetStatus)
        {
            case OfferStatus.Paused:
                transitionResult = offer.Pause(now);
                break;

            case OfferStatus.Published:
                transitionResult = offer.Resume(now);
                break;

            case OfferStatus.Archived:
                int activeClaimCount = await _offerClaimRepository.GetActiveClaimCountAsync(offer.Id, cancellationToken);
                if (activeClaimCount > 0)
                {
                    return Result.Failure<OfferDto>(Error.Conflict("Offer.ActiveClaimsExist", $"No se puede archivar la oferta porque tiene {activeClaimCount} apartados activos pendientes de canje o expiración."));
                }
                transitionResult = offer.Archive(now);
                break;

            default:
                return Result.Failure<OfferDto>(Error.Conflict("Offer.InvalidTransition", $"Transición de '{previousStatus}' a '{targetStatus}' no permitida."));
        }

        if (transitionResult.IsFailure)
        {
            return Result.Failure<OfferDto>(transitionResult.Error);
        }

        await _offerRepository.UpdateAsync(offer, cancellationToken);

        _logger.LogInformation(
            "Oferta {OfferId} transicionada de {PreviousStatus} a {NewStatus} por el comercio {ComercioId} a las {NowUtc}.",
            offer.Id, previousStatus, offer.Status, comercio.Id, now);

        return Result.Success(offer.ToDto());
    }
}
