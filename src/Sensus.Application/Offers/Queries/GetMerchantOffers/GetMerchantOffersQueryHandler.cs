using MediatR;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Offers.DTOs;
using Sensus.Domain.Common;
using Sensus.Domain.Entities;
using Sensus.Domain.Repositories;

namespace Sensus.Application.Offers.Queries.GetMerchantOffers;

public sealed class GetMerchantOffersQueryHandler : IRequestHandler<GetMerchantOffersQuery, Result<IReadOnlyList<OfferDto>>>
{
    private readonly IOfferRepository _offerRepository;
    private readonly IComercioRepository _comercioRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IOfferLikeRepository? _offerLikeRepository;

    public GetMerchantOffersQueryHandler(
        IOfferRepository offerRepository,
        IComercioRepository comercioRepository,
        ICurrentUserService currentUserService,
        IOfferLikeRepository? offerLikeRepository = null)
    {
        _offerRepository = offerRepository;
        _comercioRepository = comercioRepository;
        _currentUserService = currentUserService;
        _offerLikeRepository = offerLikeRepository;
    }

    public async Task<Result<IReadOnlyList<OfferDto>>> Handle(GetMerchantOffersQuery request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_currentUserService.UserId) || !Guid.TryParse(_currentUserService.UserId, out var userId))
        {
            return Result.Failure<IReadOnlyList<OfferDto>>(Error.Unauthorized("Offer.Unauthorized", "Debe iniciar sesión para ver sus ofertas."));
        }

        Comercio? comercio = await _comercioRepository.GetByUsuarioIdAsync(userId, cancellationToken);
        if (comercio is null || !comercio.Activo)
        {
            return Result.Failure<IReadOnlyList<OfferDto>>(Error.Forbidden("Offer.NoActiveMerchant", "El usuario no tiene un comercio activo registrado."));
        }

        IReadOnlyList<Offer> offers = await _offerRepository.GetByMerchantIdAsync(comercio!.Id, cancellationToken);

        if (!string.IsNullOrWhiteSpace(request.StatusFilter))
        {
            offers = offers
                .Where(o => string.Equals(o.Status.ToString(), request.StatusFilter.Trim(), StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        List<OfferDto> dtos = offers.Select(o => o.ToDto()).ToList();
        if (_offerLikeRepository is not null && dtos.Count > 0)
        {
            var counts = await _offerLikeRepository.CountByOfferIdsAsync(dtos.Select(d => d.Id), cancellationToken);
            var liked = await _offerLikeRepository.GetLikedOfferIdsAsync(userId, dtos.Select(d => d.Id), cancellationToken);
            dtos = dtos
                .Select(d => d with
                {
                    LikesCount = counts.GetValueOrDefault(d.Id, 0),
                    IsLikedByMe = liked.Contains(d.Id),
                })
                .ToList();
        }

        return Result.Success<IReadOnlyList<OfferDto>>(dtos);
    }
}
