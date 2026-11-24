using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Options;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Common.Settings;
using Sensus.Application.Offers.DTOs;
using Sensus.Domain.Common;
using Sensus.Domain.Entities;
using Sensus.Domain.Enums;
using Sensus.Domain.Models;
using Sensus.Domain.Repositories;
using Sensus.Domain.Services;

namespace Sensus.Application.Offers.Queries.GetOfferFeed;

public sealed class GetOfferFeedQueryHandler : IRequestHandler<GetOfferFeedQuery, Result<OfferFeedPageDto>>
{
    private readonly IOfferRepository _offerRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTimeProvider _clock;
    private readonly FeedRankingSettings _settings;
    private readonly IUserInterestsRepository? _userInterestsRepository;
    private readonly IUserAffinityReader? _affinityReader;
    private readonly IFeedSnapshotStore? _snapshotStore;
    private readonly IOfferInteractionRecorder? _interactionRecorder;
    private readonly IFeedMetricsPublisher? _metricsPublisher;

    public GetOfferFeedQueryHandler(
        IOfferRepository offerRepository,
        ICurrentUserService currentUserService,
        IDateTimeProvider clock,
        IOptions<FeedRankingSettings>? settings = null,
        IUserInterestsRepository? userInterestsRepository = null,
        IUserAffinityReader? affinityReader = null,
        IFeedSnapshotStore? snapshotStore = null,
        IOfferInteractionRecorder? interactionRecorder = null,
        IFeedMetricsPublisher? metricsPublisher = null)
    {
        _offerRepository = offerRepository;
        _currentUserService = currentUserService;
        _clock = clock;
        _settings = settings?.Value ?? new FeedRankingSettings();
        _userInterestsRepository = userInterestsRepository;
        _affinityReader = affinityReader;
        _snapshotStore = snapshotStore;
        _interactionRecorder = interactionRecorder;
        _metricsPublisher = metricsPublisher;
    }

    public async Task<Result<OfferFeedPageDto>> Handle(
        GetOfferFeedQuery request,
        CancellationToken cancellationToken)
    {
        if (_currentUserService.IsGuest)
        {
            return Result.Failure<OfferFeedPageDto>(
                Error.Forbidden("OfferFeed.GuestForbidden", "El feed de promociones está disponible solo para cuentas registradas."));
        }

        if (string.IsNullOrWhiteSpace(_currentUserService.UserId) ||
            !Guid.TryParse(_currentUserService.UserId, out Guid userId) ||
            userId == Guid.Empty)
        {
            return Result.Failure<OfferFeedPageDto>(
                Error.Unauthorized("OfferFeed.Unauthorized", "Se requiere autenticación para consultar el feed."));
        }

        FeedRankingPolicy policy = _settings.ToDomainPolicy();
        Result policyCheck = policy.Validate();
        if (policyCheck.IsFailure)
        {
            return Result.Failure<OfferFeedPageDto>(policyCheck.Error);
        }

        long startedAt = Stopwatch.GetTimestamp();
        DateTime nowUtc = _clock.UtcNow;

        if (request.SnapshotId.HasValue && _snapshotStore is not null)
        {
            FeedSnapshot? existing = await _snapshotStore.GetAsync(userId, request.SnapshotId.Value, cancellationToken);
            if (existing is not null)
            {
                OfferFeedPageDto hitPage = await ServeSnapshotPageAsync(
                    userId,
                    request,
                    existing,
                    nowUtc,
                    cancellationToken);

                await EmitImpressionsAsync(userId, hitPage, cancellationToken);
                _metricsPublisher?.PublishFeedPageServed(
                    isSnapshotHit: true,
                    snapshotSize: existing.Entries.Count,
                    elapsedMs: Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds,
                    relaxedDiversity: false,
                    relaxedExploration: false,
                    cacheDegraded: false,
                    rankingVersion: existing.RankingVersion);

                return Result.Success(hitPage);
            }
        }

        // Si se solicitó un snapshotId que venció o no existe, CA-042-06 reinicia desde la página 1.
        int effectivePage = request.SnapshotId.HasValue ? 1 : request.Page;
        int clampedRadiusKm = Math.Clamp(
            request.RadiusKm > 0 ? request.RadiusKm : _settings.DefaultRadiusKm,
            1,
            _settings.MaxRadiusKm > 0 ? _settings.MaxRadiusKm : 50);

        IReadOnlyList<FeedOfferCandidate> candidates = await _offerRepository.GetFeedCandidatesAsync(
            request.Lat,
            request.Lng,
            clampedRadiusKm * 1000,
            userId,
            policy.MaxSnapshotOffers,
            nowUtc,
            cancellationToken);

        UserInterests? interests = _userInterestsRepository is null
            ? null
            : await _userInterestsRepository.GetByUserIdAsync(userId, cancellationToken);

        IReadOnlyDictionary<string, double> affinities = _affinityReader is null
            ? new Dictionary<string, double>()
            : await _affinityReader.GetCategoryAffinitiesAsync(userId, cancellationToken);

        FeedRankResult rankResult = OfferFeedRanker.Rank(
            userId,
            candidates,
            interests?.Categories ?? Array.Empty<OfferCategory>(),
            affinities,
            clampedRadiusKm,
            nowUtc,
            policy);

        Guid candidateSnapshotId = Guid.NewGuid();
        var snapshotEntries = rankResult.Entries
            .Select(e => new FeedSnapshotEntry(e.Candidate.OfferId, e.Slot))
            .ToList();

        bool cacheSaved = false;
        bool cacheDegraded = false;
        if (_snapshotStore is not null)
        {
            int ttlMinutes = _settings.SnapshotTtlMinutes > 0 ? _settings.SnapshotTtlMinutes : 10;
            var snapshot = new FeedSnapshot(candidateSnapshotId, policy.Version, nowUtc, snapshotEntries);
            cacheSaved = await _snapshotStore.SaveAsync(userId, snapshot, TimeSpan.FromMinutes(ttlMinutes), cancellationToken);
            cacheDegraded = !cacheSaved;
        }
        else
        {
            cacheDegraded = true;
        }

        Guid? returnedSnapshotId = cacheSaved ? candidateSnapshotId : null;
        int totalCount = rankResult.Entries.Count;
        int totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)request.PageSize);

        List<OfferFeedItemDto> pageItems = rankResult.Entries
            .Skip((effectivePage - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(e => e.Candidate.ToFeedItemDto(e.Slot))
            .ToList();

        var pageDto = new OfferFeedPageDto(
            pageItems,
            effectivePage,
            request.PageSize,
            totalCount,
            totalPages,
            returnedSnapshotId,
            policy.Version);

        await EmitImpressionsAsync(userId, pageDto, cancellationToken);
        _metricsPublisher?.PublishFeedPageServed(
            isSnapshotHit: false,
            snapshotSize: totalCount,
            elapsedMs: Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds,
            relaxedDiversity: rankResult.RelaxedDiversity,
            relaxedExploration: rankResult.RelaxedExploration,
            cacheDegraded: cacheDegraded,
            rankingVersion: policy.Version);

        return Result.Success(pageDto);
    }

    private async Task<OfferFeedPageDto> ServeSnapshotPageAsync(
        Guid userId,
        GetOfferFeedQuery request,
        FeedSnapshot snapshot,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        List<FeedSnapshotEntry> slice = snapshot.Entries
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToList();

        List<OfferFeedItemDto> items = [];
        if (slice.Count > 0)
        {
            IReadOnlyList<FeedOfferCandidate> liveCandidates = await _offerRepository.GetFeedCandidatesByIdsAsync(
                slice.Select(s => s.OfferId).ToList(),
                request.Lat,
                request.Lng,
                userId,
                nowUtc,
                cancellationToken);

            Dictionary<Guid, FeedOfferCandidate> byId = liveCandidates.ToDictionary(c => c.OfferId);
            foreach (FeedSnapshotEntry entry in slice)
            {
                if (byId.TryGetValue(entry.OfferId, out FeedOfferCandidate? candidate))
                {
                    items.Add(candidate.ToFeedItemDto(entry.Slot));
                }
            }
        }

        int totalCount = snapshot.Entries.Count;
        int totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)request.PageSize);

        return new OfferFeedPageDto(
            items,
            request.Page,
            request.PageSize,
            totalCount,
            totalPages,
            snapshot.SnapshotId,
            snapshot.RankingVersion);
    }

    private async Task EmitImpressionsAsync(
        Guid userId,
        OfferFeedPageDto page,
        CancellationToken cancellationToken)
    {
        if (_interactionRecorder is null || page.Items.Count == 0)
        {
            return;
        }

        var items = new List<OfferInteractionItem>(page.Items.Count);
        int baseOffset = (page.Page - 1) * page.PageSize;

        for (int i = 0; i < page.Items.Count; i++)
        {
            OfferFeedItemDto dto = page.Items[i];
            FeedSlot slot = OfferInteractionCatalog.ParseSlot(dto.Slot);
            DistanceBucket bucket = OfferInteractionCatalog.ClassifyDistance(dto.DistanceMeters / 1000d);

            Result<OfferInteractionItem> itemResult = OfferInteractionItem.Create(
                dto.OfferId,
                baseOffset + i + 1,
                slot,
                dto.Category,
                bucket);

            if (itemResult.IsSuccess)
            {
                items.Add(itemResult.Value);
            }
        }

        if (items.Count > 0)
        {
            await _interactionRecorder.RecordImpressionsAsync(
                userId,
                page.SnapshotId?.ToString(),
                page.RankingVersion,
                page.Page,
                items,
                cancellationToken);
        }
    }
}
