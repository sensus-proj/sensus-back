using MediatR;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Common.Models;
using Sensus.Application.Offers.DTOs;
using Sensus.Domain.Common;
using Sensus.Domain.Enums;
using Sensus.Domain.Models;
using Sensus.Domain.Policies;
using Sensus.Domain.Repositories;

namespace Sensus.Application.Offers.Queries.GetNearbyOffers;

/// <summary>
/// FR-007-01 a FR-007-09: consulta las ofertas publicadas dentro de un radio de la ubicación
/// enviada, con filtro opcional por categoría del comercio y paginación estándar.
/// </summary>
public sealed record GetNearbyOffersQuery(
    double Latitude,
    double Longitude,
    double? RadiusKm = null,
    string? Category = null,
    int Page = 1,
    int PageSize = GetNearbyOffersQueryValidator.DefaultPageSize)
    : IRequest<Result<PagedResult<NearbyOfferDto>>>;

public sealed class GetNearbyOffersQueryHandler
    : IRequestHandler<GetNearbyOffersQuery, Result<PagedResult<NearbyOfferDto>>>
{
    private readonly IOfferRepository _offerRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ICacheService? _cacheService;
    private readonly IOfferLikeRepository? _offerLikeRepository;

    public GetNearbyOffersQueryHandler(
        IOfferRepository offerRepository,
        ICurrentUserService currentUserService,
        IDateTimeProvider dateTimeProvider,
        ICacheService? cacheService = null,
        IOfferLikeRepository? offerLikeRepository = null)
    {
        _offerRepository = offerRepository;
        _currentUserService = currentUserService;
        _dateTimeProvider = dateTimeProvider;
        _cacheService = cacheService;
        _offerLikeRepository = offerLikeRepository;
    }

    public async Task<Result<PagedResult<NearbyOfferDto>>> Handle(
        GetNearbyOffersQuery request,
        CancellationToken cancellationToken)
    {
        MerchantCategory? category = null;
        if (!string.IsNullOrWhiteSpace(request.Category))
        {
            if (!MerchantCategoryCatalog.TryParse(request.Category, out var parsedCategory))
            {
                return Result.Failure<PagedResult<NearbyOfferDto>>(
                    Error.Validation(
                        "Offer.CategoryInvalid",
                        $"La categoría debe pertenecer al catálogo: {string.Join(", ", MerchantCategoryCatalog.Names)}."));
            }

            category = parsedCategory;
        }

        Result<int> radiusResult = NearbyOffersRadiusPolicy.ResolveRadiusMeters(
            _currentUserService.IsGuest,
            request.RadiusKm);

        if (radiusResult.IsFailure)
        {
            return Result.Failure<PagedResult<NearbyOfferDto>>(radiusResult.Error);
        }

        // FR-048-05: la clave incluye el radio ya resuelto (invitado o registrado), la categoría y la página.
        string cacheKey = OfferCacheKeys.Nearby(
            request.Latitude,
            request.Longitude,
            radiusResult.Value,
            request.Category,
            request.Page,
            request.PageSize);

        if (_cacheService is not null)
        {
            PagedResult<NearbyOfferDto>? cached = await _cacheService.GetAsync<PagedResult<NearbyOfferDto>>(
                cacheKey,
                cancellationToken);
            if (cached is not null)
            {
                return Result.Success(await EnrichLikesAsync(cached, cancellationToken));
            }
        }

        NearbyOffersPage page = await _offerRepository.GetNearbyAsync(
            request.Latitude,
            request.Longitude,
            radiusResult.Value,
            category,
            request.Page,
            request.PageSize,
            _dateTimeProvider.UtcNow,
            cancellationToken);

        var items = page.Items.Select(ToDto).ToList();

        var result = new PagedResult<NearbyOfferDto>(
            items,
            page.TotalCount,
            request.Page,
            request.PageSize);

        if (_cacheService is not null)
        {
            await _cacheService.SetForEntityAsync(cacheKey, result, OfferCacheKeys.EntityName, cancellationToken);
        }

        return Result.Success(await EnrichLikesAsync(result, cancellationToken));
    }

    private async Task<PagedResult<NearbyOfferDto>> EnrichLikesAsync(
        PagedResult<NearbyOfferDto> page,
        CancellationToken cancellationToken)
    {
        if (_offerLikeRepository is null || page.Items.Count == 0)
        {
            return page;
        }

        // FR-055-02: Enriquecer ofertas cercanas con likes públicos reales.
        var offerIds = page.Items.Select(o => o.Id).ToList();
        var counts = await _offerLikeRepository.CountByOfferIdsAsync(offerIds, cancellationToken) ?? [];
        HashSet<Guid> likedByMe = (!_currentUserService.IsGuest
            && Guid.TryParse(_currentUserService.UserId, out Guid userId)
            ? await _offerLikeRepository.GetLikedOfferIdsAsync(userId, offerIds, cancellationToken)
            : null) ?? [];

        var enriched = page.Items
            .Select(item => item with
            {
                LikesCount = counts.GetValueOrDefault(item.Id, 0),
                IsLikedByMe = likedByMe.Contains(item.Id),
            })
            .ToList();

        return new PagedResult<NearbyOfferDto>(enriched, page.TotalCount, page.PageNumber, page.PageSize);
    }

    private static NearbyOfferDto ToDto(NearbyOfferHit hit) =>
        new(
            hit.Offer.Id,
            hit.Offer.MerchantId,
            hit.MerchantName,
            string.IsNullOrWhiteSpace(hit.Offer.Category)
                ? hit.MerchantCategory.ToString()
                : hit.Offer.Category,
            hit.Offer.Title,
            hit.Offer.Description,
            hit.Offer.OriginalPrice,
            hit.Offer.DiscountPercentage,
            hit.Offer.DiscountedPrice,
            hit.Offer.Stock,
            hit.Offer.ImageUrl,
            hit.Offer.StartsAt,
            hit.Offer.EndsAt,
            Math.Round(hit.DistanceMeters, 2),
            hit.Offer.Latitude,
            hit.Offer.Longitude);
}
