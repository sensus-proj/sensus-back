using MediatR;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Offers.DTOs;
using Sensus.Domain.Common;

namespace Sensus.Application.Offers.Queries.GetOfferFeed;

/// <summary>
/// Consulta el feed de promociones paginado y ordenado por intereses, afinidad, cercanía, urgencia y popularidad
/// (Spec 042, FR-042-01). Implementa <see cref="ISensitiveRequest"/> para no registrar coordenadas en logs.
/// </summary>
public sealed record GetOfferFeedQuery(
    double Lat,
    double Lng,
    int RadiusKm = 10,
    int Page = 1,
    int PageSize = 20,
    Guid? SnapshotId = null) : IRequest<Result<OfferFeedPageDto>>, ISensitiveRequest;
