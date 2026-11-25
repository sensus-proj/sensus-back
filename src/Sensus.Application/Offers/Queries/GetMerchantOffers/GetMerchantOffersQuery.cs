using MediatR;
using Sensus.Application.Common.Models;
using Sensus.Application.Offers.DTOs;
using Sensus.Domain.Common;

namespace Sensus.Application.Offers.Queries.GetMerchantOffers;

/// <summary>
/// SE-026 y Spec 037: Consulta paginada de ofertas del comercio autenticado, con filtro opcional por
/// estado y métricas de apartados, canjes y vistas para el inventario.
/// </summary>
public sealed record GetMerchantOffersQuery(
    string? StatusFilter = null,
    int Page = PaginationPolicy.DefaultPageNumber,
    int PageSize = PaginationPolicy.DefaultPageSize)
    : IRequest<Result<PagedResult<MerchantOfferDto>>>;
