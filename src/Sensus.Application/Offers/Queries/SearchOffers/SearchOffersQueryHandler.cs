using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Common.Models;
using Sensus.Application.Common.Settings;
using Sensus.Application.Offers.DTOs;
using Sensus.Domain.Common;
using Sensus.Domain.Models;
using Sensus.Domain.Policies;
using Sensus.Domain.Repositories;
using Sensus.Domain.Services;

namespace Sensus.Application.Offers.Queries.SearchOffers;

/// <summary>
/// Caso de uso de búsqueda híbrida determinista de ofertas por texto y cercanía (Spec 041).
/// </summary>
public sealed class SearchOffersQueryHandler
    : IRequestHandler<SearchOffersQuery, Result<PagedResult<OfferSearchItemDto>>>
{
    private readonly IOfferRepository _offerRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly OfferSearchSettings _settings;
    private readonly ILogger<SearchOffersQueryHandler> _logger;

    public SearchOffersQueryHandler(
        IOfferRepository offerRepository,
        ICurrentUserService currentUserService,
        IDateTimeProvider dateTimeProvider,
        IOptions<OfferSearchSettings> settings,
        ILogger<SearchOffersQueryHandler> logger)
    {
        _offerRepository = offerRepository;
        _currentUserService = currentUserService;
        _dateTimeProvider = dateTimeProvider;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<Result<PagedResult<OfferSearchItemDto>>> Handle(
        SearchOffersQuery request,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        string? trimmedQuery = string.IsNullOrWhiteSpace(request.Query)
            ? null
            : request.Query.Trim();
        string? normalizedQuery = trimmedQuery is null
            ? null
            : OfferTextNormalizer.Normalize(trimmedQuery);

        if (normalizedQuery is not null && normalizedQuery.Length == 1)
        {
            return Result.Failure<PagedResult<OfferSearchItemDto>>(
                Error.Validation(
                    "OfferSearch.QueryTooShort",
                    "La consulta debe estar vacía o tener al menos 2 caracteres visibles."));
        }

        double defaultRadiusKm = _settings.DefaultRadiusKm is >= 1d and <= 50d ? _settings.DefaultRadiusKm : 10d;
        double requestedRadiusKm = request.RadiusKm ?? defaultRadiusKm;
        int requestedMeters = (int)Math.Round(requestedRadiusKm * 1000d, MidpointRounding.AwayFromZero);

        int guestMaxMeters = _settings.GuestMaxRadiusKm is >= 1d and <= 50d
            ? (int)Math.Round(_settings.GuestMaxRadiusKm * 1000d, MidpointRounding.AwayFromZero)
            : GuestRadiusPolicy.GuestMaxRadiusMeters;

        int effectiveRadiusMeters = _currentUserService.IsGuest
            ? GuestRadiusPolicy.ResolveRadius(isGuest: true, Math.Min(requestedMeters, guestMaxMeters))
            : GuestRadiusPolicy.ResolveRadius(isGuest: false, requestedMeters);

        int maxCandidates = Math.Clamp(_settings.MaxCandidates > 0 ? _settings.MaxCandidates : 200, request.PageSize, 500);

        IReadOnlyList<OfferSearchCandidate> candidates = await _offerRepository.SearchOffersHybridAsync(
            normalizedQuery,
            trimmedQuery,
            request.Lat,
            request.Lng,
            effectiveRadiusMeters,
            maxCandidates,
            _dateTimeProvider.UtcNow,
            cancellationToken);

        var weights = new OfferSearchWeights(
            _settings.Weights.Text,
            _settings.Weights.CategoryMatch,
            _settings.Weights.Proximity,
            _settings.Weights.Popularity);

        var policy = new OfferSearchPolicy(weights, effectiveRadiusMeters);
        IReadOnlyList<RankedOfferSearchEntry> ranked = OfferSearchRanker.Rank(trimmedQuery, candidates, policy);

        int totalCount = ranked.Count;
        List<OfferSearchItemDto> pageItems = ranked
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(e => e.ToDto())
            .ToList();

        stopwatch.Stop();
        _logger.LogInformation(
            "Búsqueda de ofertas completada. Version={Version} QueryLength={QueryLength} EmptyQuery={EmptyQuery} RadiusMeters={RadiusMeters} IsGuest={IsGuest} TotalCount={TotalCount} ReturnedCount={ReturnedCount} ElapsedMs={ElapsedMs}",
            _settings.Version,
            trimmedQuery?.Length ?? 0,
            normalizedQuery is null,
            effectiveRadiusMeters,
            _currentUserService.IsGuest,
            totalCount,
            pageItems.Count,
            stopwatch.ElapsedMilliseconds);

        return Result.Success(new PagedResult<OfferSearchItemDto>(
            pageItems,
            totalCount,
            request.Page,
            request.PageSize));
    }
}
