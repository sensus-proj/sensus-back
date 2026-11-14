using MediatR;
using Sensus.Application.Offers.DTOs;
using Sensus.Domain.Common;

namespace Sensus.Application.Offers.Commands.ClaimOffer;

/// <summary>
/// FR-033-01 a FR-033-04: aparta una unidad de la oferta y devuelve el código firmado de un solo uso.
/// </summary>
public sealed record ClaimOfferCommand(Guid OfferId) : IRequest<Result<ClaimOfferResultDto>>;
