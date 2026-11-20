using Sensus.Domain.Models;

namespace Sensus.Application.Common.Settings;

/// <summary>
/// Configuración por entorno del feed de promociones y sus pesos versionados (Spec 042, NFR-042-07).
/// </summary>
public sealed class FeedRankingSettings
{
    public const string SectionName = "FeedRanking";

    public string Version { get; set; } = "feed-v1";
    public FeedWeightsSettings Weights { get; set; } = new();
    public int MaxConsecutiveSameCategory { get; set; } = 3;
    public int ExplorationEvery { get; set; } = 5;
    public int NewOfferHours { get; set; } = 48;
    public int SnapshotTtlMinutes { get; set; } = 10;
    public int AffinityCacheMinutes { get; set; } = 10;
    public int DefaultRadiusKm { get; set; } = 10;
    public int MaxRadiusKm { get; set; } = 50;
    public int MaxSnapshotOffers { get; set; } = 500;

    public FeedRankingPolicy ToDomainPolicy() =>
        new(
            Version,
            new FeedRankingWeights(
                Weights.Interest,
                Weights.Affinity,
                Weights.Proximity,
                Weights.Urgency,
                Weights.Popularity),
            MaxConsecutiveSameCategory,
            ExplorationEvery,
            NewOfferHours,
            MaxSnapshotOffers);
}

public sealed class FeedWeightsSettings
{
    public double Interest { get; set; } = FeedRankingWeights.Default.Interest;
    public double Affinity { get; set; } = FeedRankingWeights.Default.Affinity;
    public double Proximity { get; set; } = FeedRankingWeights.Default.Proximity;
    public double Urgency { get; set; } = FeedRankingWeights.Default.Urgency;
    public double Popularity { get; set; } = FeedRankingWeights.Default.Popularity;
}
