using System.Globalization;

namespace Sensus.Application.Feed;

/// <summary>
/// Claves de caché del feed social (Spec 026, FR-026-06).
/// Toda escritura que cambia la visibilidad o el contenido público de una publicación invalida el prefijo
/// <see cref="NearbyPrefix"/> para que la siguiente lectura refleje el cambio (CA-026-06).
/// </summary>
public static class FeedCacheKeys
{
    public const string NearbyPrefix = "feed:nearby:";

    /// <summary>Nombre de entidad para el TTL configurado en <c>Redis:TtlByEntityMinutes</c>.</summary>
    public const string NearbyCacheEntity = "Feed";

    public static string Nearby(double latitude, double longitude, double radiusKm, int page, int pageSize) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"{NearbyPrefix}{latitude:F4}:{longitude:F4}:{radiusKm:F2}:{page}:{pageSize}");
}
