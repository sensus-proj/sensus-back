using MediatR;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Common.Models;
using Sensus.Application.Offers.DTOs;
using Sensus.Domain.Common;
using Sensus.Domain.Models;
using Sensus.Domain.Repositories;

namespace Sensus.Application.Offers.Queries.GetUserFavorites;

/// <summary>
/// Spec 038 (FR-038-07): consulta de forma paginada las ofertas favoritas visibles del usuario autenticado.
/// </summary>
public sealed record GetUserFavoritesQuery(
    int Page = 1,
    int PageSize = GetUserFavoritesQueryValidator.DefaultPageSize)
    : IRequest<Result<PagedResult<FavoriteOfferDto>>>, ISensitiveRequest;

public sealed class GetUserFavoritesQueryHandler
    : IRequestHandler<GetUserFavoritesQuery, Result<PagedResult<FavoriteOfferDto>>>
{
    private readonly IOfferFavoriteRepository _favoriteRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTimeProvider _clock;
    private readonly IOfferLikeRepository? _offerLikeRepository;

    public GetUserFavoritesQueryHandler(
        IOfferFavoriteRepository favoriteRepository,
        ICurrentUserService currentUserService,
        IDateTimeProvider clock,
        IOfferLikeRepository? offerLikeRepository = null)
    {
        _favoriteRepository = favoriteRepository;
        _currentUserService = currentUserService;
        _clock = clock;
        _offerLikeRepository = offerLikeRepository;
    }

    public async Task<Result<PagedResult<FavoriteOfferDto>>> Handle(
        GetUserFavoritesQuery request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_currentUserService.UserId)
            || !Guid.TryParse(_currentUserService.UserId, out Guid userId)
            || userId == Guid.Empty)
        {
            return Result.Failure<PagedResult<FavoriteOfferDto>>(
                Error.Unauthorized("Favorite.Unauthorized", "Debes iniciar sesión para consultar favoritos."));
        }

        if (_currentUserService.IsGuest)
        {
            return Result.Failure<PagedResult<FavoriteOfferDto>>(
                Error.Forbidden("Favorite.GuestForbidden", "Los invitados no tienen lista de favoritos."));
        }

        FavoriteOffersPage page = await _favoriteRepository.GetPagedByUserIdAsync(
            userId,
            request.Page,
            request.PageSize,
            _clock.UtcNow,
            cancellationToken);

        IReadOnlyList<FavoriteOfferEntry> entries = page.Items;
        Dictionary<Guid, int> likeCounts = [];
        if (_offerLikeRepository is not null && entries.Count > 0)
        {
            var offerIds = entries.Select(e => e.Offer.Id).ToList();
            likeCounts = await _offerLikeRepository.CountByOfferIdsAsync(offerIds, cancellationToken) ?? [];
        }

        List<FavoriteOfferDto> items = entries
            .Select(entry => ToDto(entry, likeCounts.GetValueOrDefault(entry.Offer.Id, 0)))
            .ToList();

        return Result.Success(new PagedResult<FavoriteOfferDto>(
            items,
            page.TotalCount,
            request.Page,
            request.PageSize));
    }

    private static FavoriteOfferDto ToDto(FavoriteOfferEntry entry, int likesCount) =>
        new(
            entry.Offer.Id,
            entry.Offer.MerchantId,
            entry.MerchantName,
            string.IsNullOrWhiteSpace(entry.Offer.Category)
                ? entry.MerchantCategory.ToString()
                : entry.Offer.Category,
            entry.Offer.Title,
            entry.Offer.Description,
            entry.Offer.OriginalPrice,
            entry.Offer.DiscountPercentage,
            entry.Offer.DiscountedPrice,
            entry.Offer.Stock,
            entry.Offer.ImageUrl,
            entry.Offer.StartsAt,
            entry.Offer.EndsAt,
            entry.Offer.Latitude,
            entry.Offer.Longitude,
            likesCount,
            entry.FavoritedAtUtc);
}
