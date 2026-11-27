using MediatR;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Common.Models;
using Sensus.Application.Offers.DTOs;
using Sensus.Domain.Common;
using Sensus.Domain.Entities;
using Sensus.Domain.Enums;
using Sensus.Domain.Models;
using Sensus.Domain.Repositories;

namespace Sensus.Application.Offers.Queries.GetMerchantOffers;

public sealed class GetMerchantOffersQueryHandler
    : IRequestHandler<GetMerchantOffersQuery, Result<PagedResult<MerchantOfferDto>>>
{
    private readonly IOfferRepository _offerRepository;
    private readonly IComercioRepository _comercioRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IOfferLikeRepository? _offerLikeRepository;
    private readonly IOfferClaimRepository? _offerClaimRepository;

    public GetMerchantOffersQueryHandler(
        IOfferRepository offerRepository,
        IComercioRepository comercioRepository,
        ICurrentUserService currentUserService,
        IOfferLikeRepository? offerLikeRepository = null,
        IOfferClaimRepository? offerClaimRepository = null)
    {
        _offerRepository = offerRepository;
        _comercioRepository = comercioRepository;
        _currentUserService = currentUserService;
        _offerLikeRepository = offerLikeRepository;
        _offerClaimRepository = offerClaimRepository;
    }

    public async Task<Result<PagedResult<MerchantOfferDto>>> Handle(
        GetMerchantOffersQuery request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_currentUserService.UserId) ||
            !Guid.TryParse(_currentUserService.UserId, out var userId))
        {
            return Result.Failure<PagedResult<MerchantOfferDto>>(
                Error.Unauthorized("Offer.Unauthorized", "Debe iniciar sesión para ver sus ofertas."));
        }

        Comercio? comercio = await _comercioRepository.GetByUsuarioIdAsync(userId, cancellationToken);
        if (comercio is null)
        {
            return Result.Failure<PagedResult<MerchantOfferDto>>(
                Error.Forbidden("Offer.NoActiveMerchant", "El usuario no tiene un comercio activo registrado."));
        }

        OfferStatus? status = null;
        if (!string.IsNullOrWhiteSpace(request.StatusFilter))
        {
            if (!Enum.TryParse<OfferStatus>(request.StatusFilter.Trim(), ignoreCase: true, out var parsedStatus))
            {
                return Result.Failure<PagedResult<MerchantOfferDto>>(
                    Error.Validation("Offer.InvalidStatus", $"El estado '{request.StatusFilter}' no es válido."));
            }

            status = parsedStatus;
        }

        // FR-037-04 y FR-037-05: el filtro, la ordenación y el límite se resuelven en MongoDB.
        var (page, pageSize) = PaginationPolicy.Normalize(request.Page, request.PageSize);
        (IReadOnlyList<Offer> offers, int totalCount) = await _offerRepository.GetByMerchantIdPagedAsync(
            comercio.Id,
            status,
            page,
            pageSize,
            cancellationToken);

        List<MerchantOfferDto> items = offers.Select(o => o.ToMerchantOfferDto()).ToList();

        if (_offerLikeRepository is not null && items.Count > 0)
        {
            // FR-057-02: enriquecer el inventario con likes reales sin N+1.
            var likeIds = items.Select(d => d.Id).ToList();
            var likeCounts = await _offerLikeRepository.CountByOfferIdsAsync(likeIds, cancellationToken) ?? [];
            var likedByMe = await _offerLikeRepository.GetLikedOfferIdsAsync(userId, likeIds, cancellationToken) ?? [];
            items = items
                .Select(d => d with
                {
                    LikesCount = likeCounts.GetValueOrDefault(d.Id, 0),
                    IsLikedByMe = likedByMe.Contains(d.Id)
                })
                .ToList();
        }

        if (_offerClaimRepository is not null && items.Count > 0)
        {
            // FR-037-03 y NFR-037-02: un único conteo batch para toda la página.
            IReadOnlyDictionary<Guid, OfferClaimCounts>? claimCounts =
                await _offerClaimRepository.CountByOfferIdsAsync(
                    items.Select(d => d.Id),
                    cancellationToken);

            items = items
                .Select(d =>
                {
                    OfferClaimCounts metrics = claimCounts is not null &&
                                               claimCounts.TryGetValue(d.Id, out var found)
                        ? found
                        : OfferClaimCounts.Empty;

                    return d with
                    {
                        ActiveClaimsCount = metrics.ActiveClaims,
                        RedemptionsCount = metrics.Redemptions
                    };
                })
                .ToList();
        }

        return Result.Success(new PagedResult<MerchantOfferDto>(items, totalCount, page, pageSize));
    }
}
