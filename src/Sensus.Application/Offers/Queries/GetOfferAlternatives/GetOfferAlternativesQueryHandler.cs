using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Common.Settings;
using Sensus.Application.Offers.DTOs;
using Sensus.Domain.Common;
using Sensus.Domain.Models;
using Sensus.Domain.Repositories;

namespace Sensus.Application.Offers.Queries.GetOfferAlternatives;

/// <summary>
/// Caso de uso que devuelve alternativas cercanas, visibles y con precio final comparable &lt;= 120 % (Spec 043).
/// Responde con 404 uniforme si la oferta origen no existe o no es visible (FR-043-06, FR-043-10).
/// </summary>
public sealed class GetOfferAlternativesQueryHandler
    : IRequestHandler<GetOfferAlternativesQuery, Result<IReadOnlyList<OfferAlternativeItemDto>>>
{
    public const string UniformNotFoundCode = "OfferAlternatives.NotFound";
    public const string UniformNotFoundMessage = "La oferta solicitada no se encuentra disponible.";

    private readonly IOfferRepository _offerRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly OfferAlternativesSettings _settings;
    private readonly ILogger<GetOfferAlternativesQueryHandler> _logger;

    public GetOfferAlternativesQueryHandler(
        IOfferRepository offerRepository,
        ICurrentUserService currentUserService,
        IDateTimeProvider dateTimeProvider,
        IOptions<OfferAlternativesSettings> settings,
        ILogger<GetOfferAlternativesQueryHandler> logger)
    {
        _offerRepository = offerRepository;
        _currentUserService = currentUserService;
        _dateTimeProvider = dateTimeProvider;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<Result<IReadOnlyList<OfferAlternativeItemDto>>> Handle(
        GetOfferAlternativesQuery request,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        int maxDistanceMeters = OfferAlternativesPolicy.ResolveMaxDistanceMeters(
            _currentUserService.IsGuest,
            _settings.RegisteredMaxDistanceMeters > 0
                ? _settings.RegisteredMaxDistanceMeters
                : OfferAlternativesPolicy.RegisteredMaxDistanceMeters);

        decimal priceRatio = _settings.ComparablePriceRatio > 0m
            ? _settings.ComparablePriceRatio
            : OfferAlternativesPolicy.DefaultComparablePriceRatio;

        OfferAlternativesLookupResult lookup = await _offerRepository.GetAlternativesAsync(
            request.OfferId,
            maxDistanceMeters,
            priceRatio,
            _dateTimeProvider.UtcNow,
            cancellationToken);

        stopwatch.Stop();

        if (!lookup.FoundAndVisible)
        {
            _logger.LogInformation(
                "Alternativas no disponibles para oferta inexistente o no visible. IsGuest={IsGuest} ElapsedMs={ElapsedMs}",
                _currentUserService.IsGuest,
                stopwatch.ElapsedMilliseconds);

            return Result.Failure<IReadOnlyList<OfferAlternativeItemDto>>(
                Error.NotFound(UniformNotFoundCode, UniformNotFoundMessage));
        }

        int maxLimit = _settings.MaxLimit > 0 ? Math.Min(_settings.MaxLimit, OfferAlternativesPolicy.MaxLimit) : OfferAlternativesPolicy.MaxLimit;
        int safeLimit = Math.Clamp(request.Limit, 1, maxLimit);

        List<OfferAlternativeItemDto> items = lookup.Candidates
            .Where(c => c.OfferId != request.OfferId)
            .OrderBy(c => c.DistanceMeters)
            .ThenByDescending(c => c.ExpiresAtUtc)
            .ThenBy(c => c.OfferId)
            .Take(safeLimit)
            .Select(c => c.ToDto())
            .ToList();

        _logger.LogInformation(
            "Alternativas resueltas. IsGuest={IsGuest} MaxDistanceMeters={MaxDistanceMeters} Count={Count} Empty={Empty} ElapsedMs={ElapsedMs}",
            _currentUserService.IsGuest,
            maxDistanceMeters,
            items.Count,
            items.Count == 0,
            stopwatch.ElapsedMilliseconds);

        return Result.Success<IReadOnlyList<OfferAlternativeItemDto>>(items);
    }
}
