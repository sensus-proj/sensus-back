using MediatR;
using Sensus.Application.Offers.DTOs;
using Sensus.Domain.Common;

namespace Sensus.Application.Offers.Commands.RepublishOffer;

/// <summary>
/// SE-026 y Spec 036: Comando para republicar rápidamente una oferta pasada (Expirada o Archivada) con nuevas fechas y stock.
/// </summary>
public sealed record RepublishOfferCommand(
    Guid OfferId,
    DateTime NewStartsAt,
    DateTime NewEndsAt,
    int NewStock) : IRequest<Result<OfferDto>>;
