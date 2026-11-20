using Microsoft.Extensions.Logging;
using Sensus.Application.Common.Interfaces;

namespace Sensus.Infrastructure.Observability;

/// <summary>
/// Publica eventos estructurados de latencia, acierto de instantánea y relajación de reglas del feed
/// sin coordenadas ni PII (Spec 042, T042-09), manteniendo las métricas personalizadas de CloudWatch
/// dentro del límite gratuito (10/10).
/// </summary>
public sealed class StructuredFeedMetricsPublisher : IFeedMetricsPublisher
{
    private readonly ILogger<StructuredFeedMetricsPublisher> _logger;

    public StructuredFeedMetricsPublisher(ILogger<StructuredFeedMetricsPublisher> logger)
    {
        _logger = logger;
    }

    public void PublishFeedPageServed(
        bool isSnapshotHit,
        int snapshotSize,
        double elapsedMs,
        bool relaxedDiversity,
        bool relaxedExploration,
        bool cacheDegraded,
        string rankingVersion)
    {
        _logger.LogInformation(
            "Metric=OfferFeedPageServed RankingVersion={RankingVersion} SnapshotHit={SnapshotHit} SnapshotSize={SnapshotSize} ElapsedMs={ElapsedMs} RelaxedDiversity={RelaxedDiversity} RelaxedExploration={RelaxedExploration} CacheDegraded={CacheDegraded}",
            rankingVersion,
            isSnapshotHit,
            snapshotSize,
            Math.Round(Math.Max(0d, elapsedMs), 2),
            relaxedDiversity,
            relaxedExploration,
            cacheDegraded);
    }
}
