using Sensus.Domain.Enums;

namespace Sensus.Application.Common.Interfaces;

/// <summary>
/// Métrica diaria agregada de calidad del feed por versión de ranking y slot (FR-055-10).
/// </summary>
public sealed record InteractionDailyQualityMetric(
    string RankingVersion,
    FeedSlot Slot,
    long Impressions,
    long Opens,
    long Claims)
{
    public double OpenRate => Impressions > 0 ? Math.Round((double)Opens / Impressions, 6) : 0d;
    public double ClaimRate => Impressions > 0 ? Math.Round((double)Claims / Impressions, 6) : 0d;
}

/// <summary>
/// Lectura diaria de ocupación en bytes de la colección <c>offer_interactions</c> y del clúster MongoDB (NFR-055-04).
/// </summary>
public sealed record InteractionStorageStats(
    long CollectionSizeBytes,
    long CollectionLimitBytes,
    long ClusterSizeBytes,
    long ClusterLimitBytes)
{
    public double CollectionUtilizationPercent => CollectionLimitBytes > 0
        ? Math.Round(CollectionSizeBytes * 100d / CollectionLimitBytes, 2)
        : 0d;

    public double ClusterUtilizationPercent => ClusterLimitBytes > 0
        ? Math.Round(ClusterSizeBytes * 100d / ClusterLimitBytes, 2)
        : 0d;

    public double MaxUtilizationPercent => Math.Max(CollectionUtilizationPercent, ClusterUtilizationPercent);
}

/// <summary>
/// Publica métricas operativas, de calidad diaria y de ocupación en Atlas M0 sin PII ni seudónimos (Spec 055).
/// </summary>
public interface IInteractionMetricsPublisher
{
    void RecordWritten(OfferInteractionType type);
    void RecordWriteFailure(OfferInteractionType type);
    void PublishDailyQuality(IReadOnlyList<InteractionDailyQualityMetric> metrics);
    void PublishStorageUtilization(InteractionStorageStats stats);
}
