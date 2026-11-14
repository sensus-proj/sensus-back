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

    public GetOfferDetailQueryHandler(
        IOfferRepository offerRepository,
        IComercioRepository comercioRepository,
        IOfferClaimRepository offerClaimRepository,
        ICurrentUserService currentUserService)
    {
        _offerRepository = offerRepository;
        _comercioRepository = comercioRepository;
        _offerClaimRepository = offerClaimRepository;
        _currentUserService = currentUserService;
    }

    public async Task<Result<OfferDetailDto>> Handle(
        GetOfferDetailQuery request,
        CancellationToken cancellationToken)
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

        bool hasActiveClaim = await HasActiveClaimAsync(offer.Id, cancellationToken);

        return Result.Success(new OfferDetailDto(
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
            hasActiveClaim,
            offer.CreatedAtUtc,
            offer.LastModifiedAtUtc));
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
