using Microsoft.Extensions.Diagnostics.HealthChecks;
using Sensus.Application.Common.Interfaces;

namespace Sensus.Api.Health;

/// <summary>
/// Estado de la caché en <c>/health/ready</c> (Spec 048, FR-048-12). La caché es accesoria y la API opera sin
/// ella (fallback a MongoDB, NFR-048-04), por eso nunca devuelve <c>Unhealthy</c>: un fallo se reporta como
/// <c>Degraded</c> y no hace caer la readiness. Solo expone el proveedor, sin detalles de conexión.
/// </summary>
public sealed class CacheHealthCheck : IHealthCheck
{
    private readonly ICacheService _cache;

    public CacheHealthCheck(ICacheService cache)
    {
        _cache = cache;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var data = new Dictionary<string, object> { ["provider"] = _cache.Provider };

        return Task.FromResult(_cache.IsAvailable
            ? HealthCheckResult.Healthy($"Caché disponible ({_cache.Provider}).", data)
            : HealthCheckResult.Degraded("Caché no disponible; la API consulta MongoDB directamente.", data: data));
    }
}
