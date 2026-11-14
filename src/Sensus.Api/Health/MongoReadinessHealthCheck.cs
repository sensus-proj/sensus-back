using Microsoft.Extensions.Diagnostics.HealthChecks;
using Sensus.Infrastructure.Persistence.Mongo;

namespace Sensus.Api.Health;

/// <summary>
/// Comprobación de readiness: la API solo está lista si MongoDB responde (FR-016-02).
/// No expone detalles de conexión en la respuesta.
/// </summary>
public sealed class MongoReadinessHealthCheck : IHealthCheck
{
    private readonly MongoDbContext _mongoDbContext;

    public MongoReadinessHealthCheck(MongoDbContext mongoDbContext)
    {
        _mongoDbContext = mongoDbContext;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        bool available = await _mongoDbContext.PingAsync(cancellationToken);
        return available
            ? HealthCheckResult.Healthy("MongoDB disponible.")
            : HealthCheckResult.Unhealthy("MongoDB no disponible.");
    }
}
