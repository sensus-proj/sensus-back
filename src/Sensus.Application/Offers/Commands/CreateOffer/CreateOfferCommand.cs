using MediatR;
using Sensus.Application.Offers.DTOs;
using Sensus.Domain.Common;

namespace Sensus.Application.Offers.Commands.CreateOffer;

/// <summary>
/// SE-022 y Spec 034: Comando para crear una oferta comercial con fecha y hora de vencimiento automática.
/// </summary>
public sealed record CreateOfferCommand(
    string Title,
    string Description,
    decimal OriginalPrice,
    int DiscountPercentage,
    int Stock,
    DateTime StartsAt,
    DateTime EndsAt,
    string? ImageUrl = null) : IRequest<Result<OfferDto>>;
