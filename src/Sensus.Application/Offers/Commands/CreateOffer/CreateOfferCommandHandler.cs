using MediatR;
using Microsoft.Extensions.Logging;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Offers.DTOs;
using Sensus.Domain.Common;
using Sensus.Domain.Entities;
using Sensus.Domain.Repositories;

namespace Sensus.Application.Offers.Commands.CreateOffer;

public sealed class CreateOfferCommandHandler : IRequestHandler<CreateOfferCommand, Result<OfferDto>>
{
    private readonly IOfferRepository _offerRepository;
    private readonly IComercioRepository _comercioRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ILogger<CreateOfferCommandHandler> _logger;

    public CreateOfferCommandHandler(
        IOfferRepository offerRepository,
        IComercioRepository comercioRepository,
        ICurrentUserService currentUserService,
        IDateTimeProvider dateTimeProvider,
        ILogger<CreateOfferCommandHandler> logger)
    {
        _offerRepository = offerRepository;
        _comercioRepository = comercioRepository;
        _currentUserService = currentUserService;
        _dateTimeProvider = dateTimeProvider;
        _logger = logger;
    }

    public async Task<Result<OfferDto>> Handle(CreateOfferCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_currentUserService.UserId) || !Guid.TryParse(_currentUserService.UserId, out var userId))
        {
            return Result.Failure<OfferDto>(Error.Unauthorized("Offer.Unauthorized", "Debe iniciar sesión para publicar ofertas."));
        }

        Comercio? comercio = await _comercioRepository.GetByUsuarioIdAsync(userId, cancellationToken);
        if (comercio is null || !comercio.Activo)
        {
            return Result.Failure<OfferDto>(Error.Forbidden("Offer.NoActiveMerchant", "El usuario no tiene un comercio activo registrado para publicar ofertas."));
        }

        Result<Offer> offerResult = Offer.Create(
            comercio.Id,
            request.Title,
            request.Description,
            request.OriginalPrice,
            request.DiscountPercentage,
            request.Stock,
            request.StartsAt,
            request.EndsAt,
            request.ImageUrl,
            comercio.Latitud,
            comercio.Longitud,
            _dateTimeProvider.UtcNow);

        if (offerResult.IsFailure)
        {
            return Result.Failure<OfferDto>(offerResult.Error);
        }

        Offer offer = offerResult.Value;
        await _offerRepository.AddAsync(offer, cancellationToken);

        _logger.LogInformation("Oferta {OfferId} creada exitosamente para el comercio {ComercioId}. Vigencia hasta {EndsAtUtc}",
            offer.Id, comercio.Id, offer.EndsAt);

        return Result.Success(offer.ToDto());
    }
}
