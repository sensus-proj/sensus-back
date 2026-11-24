using FluentValidation;
using MediatR;
using Sensus.Application.Comercios.DTOs;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Common.Models;
using Sensus.Domain.Common;
using Sensus.Domain.Entities;
using Sensus.Domain.Repositories;

namespace Sensus.Application.Comercios.Queries.GetStoreReviews;

/// <summary>
/// Obtiene el resumen estilo Play Store (promedio de estrellas, desglose 5..1) y comentarios paginados de un comercio (Spec 010 / T056-05).
/// </summary>
public sealed record GetStoreReviewsQuery(
    Guid MerchantId,
    int Page = PaginationPolicy.DefaultPageNumber,
    int PageSize = PaginationPolicy.DefaultPageSize) : IRequest<Result<StoreReviewsSummaryDto>>;

public sealed class GetStoreReviewsQueryValidator : AbstractValidator<GetStoreReviewsQuery>
{
    public GetStoreReviewsQueryValidator()
    {
        RuleFor(x => x.MerchantId)
            .NotEmpty()
            .WithMessage("El identificador del comercio es obligatorio.");

        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1)
            .WithMessage("El número de página debe ser al menos 1.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, PaginationPolicy.MaxPageSize)
            .WithMessage($"El tamaño de página debe estar entre 1 y {PaginationPolicy.MaxPageSize}.");
    }
}

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

        StoreReviewRatingStats? ratingStats = await _storeReviewRepository.GetRatingSummaryAsync(
            comercio.Id,
            cancellationToken);

        (IReadOnlyList<StoreReview>? pagedReviews, long totalCount) = await _storeReviewRepository.GetPagedByMerchantIdAsync(
            comercio.Id,
            request.Page,
            request.PageSize,
            cancellationToken);

        if (pagedReviews is null)
        {
            var allReviews = await _storeReviewRepository.GetByMerchantIdAsync(comercio.Id, cancellationToken);
            totalCount = allReviews.Count;
            int safePage = Math.Max(1, request.Page);
            int safeSize = Math.Clamp(request.PageSize, 1, PaginationPolicy.MaxPageSize);
            pagedReviews = allReviews
                .OrderByDescending(r => r.CreatedAtUtc)
                .Skip((safePage - 1) * safeSize)
                .Take(safeSize)
                .ToList();

            if (ratingStats is null)
            {
                double avg = allReviews.Count > 0 ? Math.Round(allReviews.Average(r => r.Rating), 1) : 0.0;
                var counts = allReviews.GroupBy(r => r.Rating).ToDictionary(g => g.Key, g => g.Count());
                ratingStats = new StoreReviewRatingStats(avg, allReviews.Count, counts);
            }
        }

        ratingStats ??= new StoreReviewRatingStats(0.0, 0, new Dictionary<int, int>());

        Guid? currentUserId = !_currentUserService.IsGuest
            && Guid.TryParse(_currentUserService.UserId, out Guid parsedUserId)
            ? parsedUserId
            : null;
        return Result.Success(
            StoreReviewMappingExtensions.BuildSummary(
                comercio,
                ratingStats,
                pagedReviews,
                totalCount,
                currentUserId,
                request.Page,
                request.PageSize));
    }
}
