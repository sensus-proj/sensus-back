using MediatR;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Offers.DTOs;
using Sensus.Domain.Common;
using Sensus.Domain.Repositories;

namespace Sensus.Application.Offers.Queries.GetMerchantOffers;

public sealed class GetMerchantOffersQueryHandler : IRequestHandler<GetMerchantOffersQuery, Result<IReadOnlyList<OfferDto>>>
{
    private readonly IOfferRepository _offerRepository;
    private readonly IComercioRepository _comercioRepository;
    private readonly ICurrentUserService _currentUserService;

    public GetMerchantOffersQueryHandler(
        IOfferRepository offerRepository,
        IComercioRepository comercioRepository,
        ICurrentUserService currentUserService)
    {
        _offerRepository = offerRepository;
        _comercioRepository = comercioRepository;
        _currentUserService = currentUserService;
    }

    public async Task<Result<IReadOnlyList<OfferDto>>> Handle(GetMerchantOffersQuery request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_currentUserService.UserId) || !Guid.TryParse(_currentUserService.UserId, out var userId))
        {
            return Result.Failure<IReadOnlyList<OfferDto>>(Error.Unauthorized("Offer.Unauthorized", "Debe iniciar sesión para ver sus ofertas."));
        }

        var comercio = await _comercioRepository.GetByUsuarioIdAsync(userId, cancellationToken);
        if (comercio is null || !comercio.Activo)
        {
            return Result.Failure<IReadOnlyList<OfferDto>>(Error.Forbidden("Offer.NoActiveMerchant", "El usuario no tiene un comercio activo registrado."));
        }

        var offers = await _offerRepository.GetByMerchantIdAsync(comercio.Id, cancellationToken);

        if (!string.IsNullOrWhiteSpace(request.StatusFilter))
        {
            offers = offers
                .Where(o => string.Equals(o.Status.ToString(), request.StatusFilter.Trim(), StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        var dtos = offers.Select(o => o.ToDto()).ToList();
        return Result.Success<IReadOnlyList<OfferDto>>(dtos);
    }
}
