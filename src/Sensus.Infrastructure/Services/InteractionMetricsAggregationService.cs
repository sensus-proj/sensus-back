using Microsoft.Extensions.Options;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Common.Settings;
using Sensus.Domain.Repositories;

namespace Sensus.Infrastructure.Services;

/// <summary>
/// Calcula diariamente la calidad agregada por slot/versión y el tamaño de <c>offer_interactions</c> y del clúster M0
/// (Spec 055, FR-055-10, NFR-055-04).
/// </summary>
public sealed class InteractionMetricsAggregationService
{
    private readonly IOfferInteractionRepository _repository;
    private readonly IInteractionMetricsPublisher _metrics;
    private readonly IDateTimeProvider _clock;
    private readonly InteractionSettings _settings;

    public InteractionMetricsAggregationService(
        IOfferInteractionRepository repository,
        IInteractionMetricsPublisher metrics,
        IDateTimeProvider clock,
        IOptions<InteractionSettings> settings)
    {
        _repository = repository;
        _metrics = metrics;
        _clock = clock;
        _settings = settings.Value;
    }

    public async Task<InteractionStorageStats> PublishDailySnapshotAsync(CancellationToken cancellationToken = default)
    {
        DateTime sinceUtc = _clock.UtcNow.AddDays(-1);

        IReadOnlyList<InteractionQualityAggregate> aggregates =
            await _repository.AggregateDailyQualityAsync(sinceUtc, cancellationToken);

        IReadOnlyList<InteractionDailyQualityMetric> qualityMetrics = aggregates
            .Select(a => new InteractionDailyQualityMetric(
                a.RankingVersion,
                a.Slot,
                a.Impressions,
                a.Opens,
                a.Claims))
            .ToList();

        _metrics.PublishDailyQuality(qualityMetrics);

        InteractionCollectionStorageSize size = await _repository.GetStorageSizeAsync(cancellationToken);
        var stats = new InteractionStorageStats(
            size.CollectionBytes,
            _settings.CollectionLimitBytes,
            size.ClusterDataBytes,
            _settings.ClusterLimitBytes);

        _metrics.PublishStorageUtilization(stats);
        return stats;
    }
}
