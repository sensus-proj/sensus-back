using System.Globalization;
using Sensus.Application.Common.Interfaces;

namespace Sensus.Application.Offers;

/// <summary>
/// Claves e invalidación del caché de ofertas (Spec 048: FR-048-04, FR-048-05 y FR-048-07).
/// </summary>
public static class OfferCacheKeys
{
    /// <summary>Nombre de entidad para el TTL configurable en <c>Redis:TtlByEntityMinutes</c>.</summary>
    public const string EntityName = "Offers";

    public const string NearbyPrefix = "sensus:offers:nearby:";

    public static string Detail(Guid offerId) => $"sensus:offers:detail:{offerId}";

    public static string Nearby(
        double latitude,
        double longitude,
        int radiusMeters,
        string? category,
        int page,
        int pageSize)
    {
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{NearbyPrefix}{latitude:F5}:{longitude:F5}:{radiusMeters}:{category?.Trim().ToUpperInvariant() ?? "-"}:{page}:{pageSize}");
    }

    /// <summary>
    /// Invalida el detalle de una oferta (si se indica) y todas las consultas de ofertas cercanas.
    /// No hace nada si el caché no está disponible.
    /// </summary>
    public static async Task InvalidateAsync(
        this ICacheService? cacheService,
        Guid? offerId,
        CancellationToken cancellationToken)
    {
        if (cacheService is null)
        {
            return;
        }

        if (offerId.HasValue)
        {
            await cacheService.RemoveAsync(Detail(offerId.Value), cancellationToken);
        }

        await cacheService.RemoveByPrefixAsync(NearbyPrefix, cancellationToken);
    }
}
