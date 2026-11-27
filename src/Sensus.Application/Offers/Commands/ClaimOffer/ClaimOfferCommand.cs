using MediatR;
using Sensus.Application.Offers.DTOs;
using Sensus.Domain.Common;

namespace Sensus.Application.Offers.Commands.ClaimOffer;

/// <summary>
/// FR-033-01 a FR-033-04: aparta una o más unidades de una oferta y devuelve el código firmado de un solo uso.
/// </summary>
public sealed record ClaimOfferCommand(Guid OfferId, int Quantity = 1) : IRequest<Result<ClaimOfferResultDto>>;

public sealed record CartClaimItemInput(Guid OfferId, int Quantity = 1);

/// <summary>
/// Apartado de carrito de un mismo comercio (1+ productos y N unidades por producto) con un solo código QR.
/// </summary>
public sealed record CreateCartClaimCommand(
    IReadOnlyList<CartClaimItemInput> Items) : IRequest<Result<ClaimOfferResultDto>>;
