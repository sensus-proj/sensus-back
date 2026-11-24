using MediatR;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Common.Models;
using Sensus.Application.Offers.DTOs;
using Sensus.Domain.Common;

namespace Sensus.Application.Offers.Queries.SearchOffers;

/// <summary>
/// Consulta de búsqueda híbrida de ofertas por texto (stemming español y prefijo) y cercanía (Spec 041).
/// Implementa <see cref="ISensitiveRequest"/> para no exponer el término buscado ni coordenadas en logs globales.
/// </summary>
public sealed record SearchOffersQuery(
    string? Query,
    double Lat,
    double Lng,
    double? RadiusKm = null,
    int Page = 1,
    int PageSize = 20)
    : IRequest<Result<PagedResult<OfferSearchItemDto>>>, ISensitiveRequest;
