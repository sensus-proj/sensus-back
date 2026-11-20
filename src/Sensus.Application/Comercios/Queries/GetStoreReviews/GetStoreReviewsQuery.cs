using MediatR;
using Sensus.Application.Comercios.DTOs;
using Sensus.Application.Common.Interfaces;
using Sensus.Domain.Common;
using Sensus.Domain.Entities;
using Sensus.Domain.Repositories;

namespace Sensus.Application.Comercios.Queries.GetStoreReviews;

/// <summary>
/// Obtiene el resumen estilo Play Store (promedio de estrellas, desglose 5..1) y comentarios de un comercio.
/// </summary>
public sealed record GetStoreReviewsQuery(Guid MerchantId) : IRequest<Result<StoreReviewsSummaryDto>>;

public sealed class GetStoreReviewsQueryHandler : IRequestHandler<GetStoreReviewsQuery, Result<StoreReviewsSummaryDto>>
{
    private readonly IComercioRepository _comercioRepository;
    private readonly IStoreReviewRepository _storeReviewRepository;
    private readonly ICurrentUserService _currentUserService;

    public GetStoreReviewsQueryHandler(
        IComercioRepository comercioRepository,
        IStoreReviewRepository storeReviewRepository,
        ICurrentUserService currentUserService)
    {
        _comercioRepository = comercioRepository;
        _storeReviewRepository = storeReviewRepository;
        _currentUserService = currentUserService;
    }

    public async Task<Result<StoreReviewsSummaryDto>> Handle(
        GetStoreReviewsQuery request,
        CancellationToken cancellationToken)
    {
        Comercio? comercio = await _comercioRepository.GetByIdAsync(request.MerchantId, cancellationToken);
        if (comercio is null || !comercio.Activo)
        {
            return Result.Failure<StoreReviewsSummaryDto>(
                Error.NotFound("Comercio.NotFound", "El comercio solicitado no existe o no está disponible."));
        }

        IReadOnlyList<StoreReview> reviews = await _storeReviewRepository.GetByMerchantIdAsync(
            comercio.Id,
            cancellationToken);

        Guid? currentUserId = !_currentUserService.IsGuest
            && Guid.TryParse(_currentUserService.UserId, out Guid parsedUserId)
            ? parsedUserId
            : null;
        return Result.Success(StoreReviewMappingExtensions.BuildSummary(comercio, reviews, currentUserId));
    }
}
