using MediatR;
using Sensus.Application.Offers.DTOs;
using Sensus.Domain.Common;
using Sensus.Domain.Models;

namespace Sensus.Application.Offers.Queries.GetOfferAlternatives;

/// <summary>
/// Consulta de alternativas cercanas de la misma categoría y precio final comparable (Spec 043).
/// </summary>
public sealed record GetOfferAlternativesQuery(
    Guid OfferId,
    int Limit = OfferAlternativesPolicy.DefaultLimit)
    : IRequest<Result<IReadOnlyList<OfferAlternativeItemDto>>>;
