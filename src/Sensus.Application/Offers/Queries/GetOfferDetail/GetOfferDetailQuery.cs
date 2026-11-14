using MediatR;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Offers.DTOs;
using Sensus.Domain.Common;
using Sensus.Domain.Entities;
using Sensus.Domain.Enums;
using Sensus.Domain.Repositories;

namespace Sensus.Application.Offers.Queries.GetOfferDetail;

/// <summary>
/// FR-032-01 y FR-032-02: devuelve el detalle de una oferta visible con su comercio, vigencia,
/// stock, precio y el indicador de apartado activo del usuario.
/// </summary>
public sealed record GetOfferDetailQuery(Guid OfferId) : IRequest<Result<OfferDetailDto>>;

public sealed class GetOfferDetailQueryHandler : IRequestHandler<GetOfferDetailQuery, Result<OfferDetailDto>>
{
    private readonly IOfferRepository _offerRepository;
    private readonly IComercioRepository _comercioRepository;
    private readonly IOfferClaimRepository _offerClaimRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly ICacheService? _cacheService;

    public GetOfferDetailQueryHandler(
        IOfferRepository offerRepository,
        IComercioRepository comercioRepository,
        IOfferClaimRepository offerClaimRepository,
        ICurrentUserService currentUserService,
        ICacheService? cacheService = null)
    {
        _offerRepository = offerRepository;
        _comercioRepository = comercioRepository;
        _offerClaimRepository = offerClaimRepository;
        _currentUserService = currentUserService;
        _cacheService = cacheService;
    }

    public async Task<Result<OfferDetailDto>> Handle(
        GetOfferDetailQuery request,
        CancellationToken cancellationToken)
    {
        // FR-048-04: el detalle común a todos los usuarios se cachea; el indicador de apartado activo
        // depende de la persona autenticada y se calcula siempre en cada solicitud.
        string cacheKey = OfferCacheKeys.Detail(request.OfferId);
        OfferDetailDto? detail = _cacheService is null
            ? null
            : await _cacheService.GetAsync<OfferDetailDto>(cacheKey, cancellationToken);

        if (detail is null)
        {
            Offer? offer = await _offerRepository.GetByIdAsync(request.OfferId, cancellationToken);
            if (offer is null || offer.Status == OfferStatus.Archived)
            {
                return Result.Failure<OfferDetailDto>(NotFound());
            }

            Comercio? comercio = await _comercioRepository.GetByIdAsync(offer.MerchantId, cancellationToken);
            if (comercio is null || !comercio.Activo)
            {
                return Result.Failure<OfferDetailDto>(NotFound());
            }

            detail = new OfferDetailDto(
                offer.Id,
                offer.MerchantId,
                comercio.NombreComercial,
                comercio.Direccion,
                comercio.Categoria.ToString(),
                offer.Title,
                offer.Description,
                offer.OriginalPrice,
                offer.DiscountPercentage,
                offer.DiscountedPrice,
                offer.Stock,
                offer.Status.ToString(),
                offer.ImageUrl,
                offer.Latitude,
                offer.Longitude,
                offer.StartsAt,
                offer.EndsAt,
                offer.ViewCount,
                false,
                offer.CreatedAtUtc,
                offer.LastModifiedAtUtc);

            if (_cacheService is not null)
            {
                await _cacheService.SetForEntityAsync(cacheKey, detail, OfferCacheKeys.EntityName, cancellationToken);
            }
        }

        bool hasActiveClaim = await HasActiveClaimAsync(request.OfferId, cancellationToken);

        return Result.Success(detail with { HasActiveClaim = hasActiveClaim });
    }

    private async Task<bool> HasActiveClaimAsync(Guid offerId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_currentUserService.UserId) ||
            !Guid.TryParse(_currentUserService.UserId, out Guid userId))
        {
            return false;
        }

        OfferClaim? claim = await _offerClaimRepository.GetActiveByUserAndOfferAsync(
            userId,
            offerId,
            cancellationToken);

        return claim is not null;
    }

    private static Error NotFound() =>
        Error.NotFound("Offer.NotFound", "La oferta solicitada no existe o no está disponible.");
}
