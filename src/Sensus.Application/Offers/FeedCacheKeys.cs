namespace Sensus.Application.Offers;

/// <summary>
/// Claves de caché para afinidad e instantáneas del feed de promociones (Specs 042 y 054).
/// </summary>
public static class FeedCacheKeys
{
    /// <summary>Prefijo de todas las instantáneas del feed de ofertas; permite invalidarlas en lote (FR-048-06/08).</summary>
    public const string SnapshotPrefix = "feed:offers:";

    public static string Affinity(Guid userId) => $"feed:affinity:{userId:D}";

    public static string Snapshot(Guid userId, Guid snapshotId) => $"{SnapshotPrefix}{userId:D}:{snapshotId:D}";
}
