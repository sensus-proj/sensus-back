namespace Sensus.Application.Common.Interfaces;

/// <summary>
/// Registra métricas operativas y de degradación del feed sin coordenadas ni PII (Spec 042, T042-09).
/// </summary>
public interface IFeedMetricsPublisher
{
    void PublishFeedPageServed(
        bool isSnapshotHit,
        int snapshotSize,
        double elapsedMs,
        bool relaxedDiversity,
        bool relaxedExploration,
        bool cacheDegraded,
        string rankingVersion);
}
