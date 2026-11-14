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

    public GetNearbyOffersQueryHandler(
        IOfferRepository offerRepository,
        ICurrentUserService currentUserService,
        IDateTimeProvider dateTimeProvider)
    {
        _offerRepository = offerRepository;
        _currentUserService = currentUserService;
        _dateTimeProvider = dateTimeProvider;
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

        return Result.Success(new PagedResult<NearbyOfferDto>(
            items,
            page.TotalCount,
            request.Page,
            request.PageSize));
    }

    private static NearbyOfferDto ToDto(NearbyOfferHit hit) =>
        new(
            hit.Offer.Id,
            hit.Offer.MerchantId,
            hit.MerchantName,
            hit.MerchantCategory.ToString(),
            hit.Offer.Title,
            hit.Offer.Description,
            hit.Offer.OriginalPrice,
            hit.Offer.DiscountPercentage,
            hit.Offer.DiscountedPrice,
            hit.Offer.Stock,
            hit.Offer.ImageUrl,
            hit.Offer.StartsAt,
            hit.Offer.EndsAt,
            Math.Round(hit.DistanceMeters, 2));
}
