using MediatR;
using Sensus.Application.Offers.DTOs;
using Sensus.Domain.Common;

namespace Sensus.Application.Offers.Commands.UpdateOffer;

/// <summary>
/// Spec 035 (FR-035-01): Comando para editar una oferta existente con control de concurrencia optimista.
/// </summary>
public sealed record UpdateOfferCommand(
    Guid OfferId,
    string Title,
    string Description,
    decimal OriginalPrice,
    int DiscountPercentage,
    int Stock,
    DateTime StartsAt,
    DateTime EndsAt,
    string? ImageUrl,
    int ExpectedVersion) : IRequest<Result<OfferDto>>;
