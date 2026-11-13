using MediatR;
using Sensus.Application.Offers.DTOs;
using Sensus.Domain.Common;

namespace Sensus.Application.Offers.Queries.GetMerchantOffers;

/// <summary>
/// SE-026 y Spec 037: Consulta de ofertas del comercio autenticado con filtro opcional por estado para republicación o inventario.
/// </summary>
public sealed record GetMerchantOffersQuery(string? StatusFilter = null) : IRequest<Result<IReadOnlyList<OfferDto>>>;
