using MediatR;
using Sensus.Application.Common.Interfaces;
using Sensus.Domain.Common;
using Sensus.Domain.Entities;
using Sensus.Domain.Enums;
using Sensus.Domain.Repositories;

namespace Sensus.Application.Offers.Commands.RegisterOfferView;

public sealed class RegisterOfferViewCommandHandler : IRequestHandler<RegisterOfferViewCommand, Result>
{
    private readonly IOfferRepository _offerRepository;
    private readonly IOfferViewRepository _offerViewRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTimeProvider _dateTimeProvider;

    public RegisterOfferViewCommandHandler(
        IOfferRepository offerRepository,
        IOfferViewRepository offerViewRepository,
        ICurrentUserService currentUserService,
        IDateTimeProvider dateTimeProvider)
    {
        _offerRepository = offerRepository;
        _offerViewRepository = offerViewRepository;
        _currentUserService = currentUserService;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<Result> Handle(
        RegisterOfferViewCommand request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_currentUserService.UserId) ||
            !Guid.TryParse(_currentUserService.UserId, out Guid userId))
        {
            return Result.Failure(
                Error.Unauthorized("OfferView.Unauthorized", "Debe iniciar sesión para registrar una vista."));
        }

        Offer? offer = await _offerRepository.GetByIdAsync(request.OfferId, cancellationToken);
        if (offer is null || offer.Status == OfferStatus.Archived)
        {
            return Result.Failure(
                Error.NotFound("Offer.NotFound", "La oferta solicitada no existe o no está disponible."));
        }

        Result<OfferView> viewResult = OfferView.Create(userId, offer.Id, _dateTimeProvider.UtcNow);
        if (viewResult.IsFailure)
        {
            return viewResult;
        }

        bool registered = await _offerViewRepository.TryRegisterAsync(viewResult.Value, cancellationToken);
        if (!registered)
        {
            // Vista duplicada del mismo día: la operación es idempotente y no altera el contador.
            return Result.Success();
        }

        Result incrementResult = await IncrementCounterAsync(offer, cancellationToken);
        return incrementResult.IsFailure ? incrementResult : Result.Success();
    }

    private async Task<Result> IncrementCounterAsync(Offer offer, CancellationToken cancellationToken)
    {
        bool incremented = await _offerRepository.IncrementViewCountAsync(offer.Id, cancellationToken);
        return incremented
            ? Result.Success()
            : Result.Failure(Error.NotFound("Offer.NotFound", "La oferta solicitada no existe o no está disponible."));
    }
}
