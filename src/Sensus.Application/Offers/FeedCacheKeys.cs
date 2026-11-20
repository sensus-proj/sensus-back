namespace Sensus.Application.Offers;

/// <summary>
/// Claves de caché para afinidad e instantáneas del feed de promociones (Specs 042 y 054).
/// </summary>
public static class FeedCacheKeys
{
    public static string Affinity(Guid userId) => $"feed:affinity:{userId:D}";

    public static string Snapshot(Guid userId, Guid snapshotId) => $"feed:offers:{userId:D}:{snapshotId:D}";
}
