using MediatR;
using Sensus.Application.Comercios.DTOs;
using Sensus.Application.Common.Interfaces;
using Sensus.Domain.Common;
using Sensus.Domain.Repositories;

namespace Sensus.Application.Comercios.Queries.GetMyComercio;

/// <summary>
/// Handler para consultar el comercio propio del usuario para habilitar el panel comercial (SE-004 / Spec 048).
/// </summary>
public sealed class GetMyComercioQueryHandler : IRequestHandler<GetMyComercioQuery, Result<ComercioDto>>
{
    private readonly IComercioRepository _comercioRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly ICacheService? _cacheService;
    private readonly IStoreReviewRepository? _storeReviewRepository;

    public GetMyComercioQueryHandler(
        IComercioRepository comercioRepository,
        ICurrentUserService currentUserService,
        ICacheService? cacheService = null,
        IStoreReviewRepository? storeReviewRepository = null)
    {
        _comercioRepository = comercioRepository;
        _currentUserService = currentUserService;
        _cacheService = cacheService;
        _storeReviewRepository = storeReviewRepository;
    }

    public async Task<Result<ComercioDto>> Handle(
        GetMyComercioQuery request,
        CancellationToken cancellationToken)
    {
        if (!_currentUserService.IsAuthenticated || !Guid.TryParse(_currentUserService.UserId, out var usuarioId))
        {
            return Result.Failure<ComercioDto>(
                Error.Unauthorized("Auth.Unauthorized", "Debe iniciar sesión para consultar los datos de su comercio."));
        }

        var cacheKey = $"sensus:merchants:user:{usuarioId}";
        if (_cacheService is not null)
        {
            var cached = await _cacheService.GetAsync<ComercioDto>(cacheKey, cancellationToken);
            if (cached is not null)
            {
                return Result.Success(await EnrichRatingAsync(cached, cancellationToken));
            }
        }

        var comercio = await _comercioRepository.GetByUsuarioIdAsync(usuarioId, cancellationToken);
        if (comercio is null)
        {
            return Result.Failure<ComercioDto>(
                Error.NotFound("Comercio.NotFound", "El usuario no tiene ningún comercio registrado."));
        }

        var dto = comercio.ToDto();
        if (_cacheService is not null)
        {
            await _cacheService.SetForEntityAsync(cacheKey, dto, "MyComercio", cancellationToken);
        }

        return Result.Success(await EnrichRatingAsync(dto, cancellationToken));
    }

    private async Task<ComercioDto> EnrichRatingAsync(ComercioDto dto, CancellationToken cancellationToken)
    {
        if (_storeReviewRepository is null)
        {
            return dto;
        }

        var summaries = await _storeReviewRepository.GetRatingSummariesByMerchantIdsAsync(
            [dto.Id],
            cancellationToken);

        if (!summaries.TryGetValue(dto.Id, out var summary))
        {
            return dto with { AverageRating = 0.0, ReviewCount = 0 };
        }

        return dto with
        {
            AverageRating = Math.Round(summary.AverageRating, 1),
            ReviewCount = summary.ReviewCount,
        };
    }
}
