using Sensus.Infrastructure.Services;

namespace Sensus.Api.Services;

/// <summary>
/// Servicio periódico que emite una vez al día las métricas de calidad de interacciones y ocupación de Atlas M0
/// (Spec 055, T055-06, T055-08).
/// </summary>
public sealed class InteractionStatsBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<InteractionStatsBackgroundService> _logger;
    private readonly TimeSpan _interval = TimeSpan.FromHours(24);

    public InteractionStatsBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<InteractionStatsBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using IServiceScope scope = _scopeFactory.CreateScope();
                var aggregator = scope.ServiceProvider.GetRequiredService<InteractionMetricsAggregationService>();
                await aggregator.PublishDailySnapshotAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error al publicar métricas diarias de interacciones.");
            }

            try
            {
                await Task.Delay(_interval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}
