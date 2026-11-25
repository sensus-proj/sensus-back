using MediatR;
using Sensus.Application.Comercios.DTOs;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Common.Models;
using Sensus.Domain.Common;
using Sensus.Domain.Policies;
using Sensus.Domain.Repositories;

namespace Sensus.Application.Comercios.Queries.GetNearbyComercios;

/// <summary>
/// SE-008: Consulta comercios cercanos al usuario en base a coordenadas geográficas.
/// Aplica en servidor la restricción estricta de 2 km para sesiones de invitado.
/// </summary>
public sealed record GetNearbyComerciosQuery(
    double Latitud,
    double Longitud,
    int? RadioMetros = null,
    double? RadiusKm = null,
    int Page = 1,
    int PageSize = GetNearbyComerciosQueryValidator.DefaultPageSize) : IRequest<Result<PagedResult<ComercioDto>>>;

public sealed class GetNearbyComerciosQueryHandler : IRequestHandler<GetNearbyComerciosQuery, Result<PagedResult<ComercioDto>>>
{
    private readonly IComercioRepository _comercioRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IStoreReviewRepository? _storeReviewRepository;

    public GetNearbyComerciosQueryHandler(
        IComercioRepository comercioRepository,
        ICurrentUserService currentUserService,
        IStoreReviewRepository? storeReviewRepository = null)
    {
        _comercioRepository = comercioRepository;
        _currentUserService = currentUserService;
        _storeReviewRepository = storeReviewRepository;
    }

    public async Task<Result<PagedResult<ComercioDto>>> Handle(GetNearbyComerciosQuery request, CancellationToken cancellationToken)
    {
        var isGuest = _currentUserService.IsGuest;
        var requestedRadius = request.RadiusKm is double radiusKm
            ? (int)Math.Ceiling(Math.Min(radiusKm * 1000, GuestRadiusPolicy.RegisteredMaxRadiusMeters))
            : request.RadioMetros ?? GuestRadiusPolicy.DefaultRadiusMeters;
        var effectiveRadius = GuestRadiusPolicy.ResolveRadius(isGuest, requestedRadius);

        var (comercios, totalCount) = await _comercioRepository.GetNearbyAsync(
            request.Latitud,
            request.Longitud,
            effectiveRadius,
            request.Page,
            request.PageSize,
            cancellationToken);

        var dtos = comercios.Select(c => c.ToDto()).ToList();
        if (_storeReviewRepository is not null && dtos.Count > 0)
        {
            // FR-056-06: Poblar promedio y conteo real en comercios cercanos.
            var summaries = await _storeReviewRepository.GetRatingSummariesByMerchantIdsAsync(
                dtos.Select(d => d.Id),
                cancellationToken) ?? [];

            dtos = dtos
                .Select(d => summaries.TryGetValue(d.Id, out var summary)
                    ? d with { AverageRating = Math.Round(summary.AverageRating, 1), ReviewCount = summary.ReviewCount }
                    : d)
                .ToList();
        }

        return Result.Success(new PagedResult<ComercioDto>(dtos, totalCount, request.Page, request.PageSize));
    }
}
