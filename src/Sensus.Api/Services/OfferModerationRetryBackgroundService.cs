using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Sensus.Application.Common.Settings;
using Sensus.Application.Offers.Commands.ProcessPendingOfferModeration;
using Sensus.Domain.Common;

namespace Sensus.Api.Services;

/// <summary>
/// Re-evalúa ofertas en estado <c>PendingModeration</c> cuando el moderador vuelve a estar disponible
/// (Spec 034, FR-034-07).
/// </summary>
public sealed class OfferModerationRetryBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OfferModerationRetryBackgroundService> _logger;
    private readonly TimeSpan _checkInterval;

    public OfferModerationRetryBackgroundService(
        IServiceScopeFactory scopeFactory,
        IOptions<OfferModerationRetrySettings> settings,
        ILogger<OfferModerationRetryBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        int intervalSeconds = Math.Clamp(settings.Value.CheckIntervalSeconds, 5, 3600);
        _checkInterval = TimeSpan.FromSeconds(intervalSeconds);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Servicio de re-moderación diferida de ofertas iniciado. Intervalo: {Interval}",
            _checkInterval);

        try
        {
            await Task.Delay(TimeSpan.FromSeconds(20), stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using IServiceScope scope = _scopeFactory.CreateScope();
                ISender sender = scope.ServiceProvider.GetRequiredService<ISender>();
                Result<ProcessPendingOfferModerationResultDto> result =
                    await sender.Send(new ProcessPendingOfferModerationCommand(), stoppingToken);

                if (result.IsSuccess && result.Value.CheckedCount > 0)
                {
                    _logger.LogInformation(
                        "Re-moderación de ofertas: {Checked} revisadas ({Published} publicadas, {Rejected} rechazadas, {Rescheduled} reprogramadas).",
                        result.Value.CheckedCount,
                        result.Value.PublishedCount,
                        result.Value.RejectedCount,
                        result.Value.RescheduledCount);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error durante la ejecución del job de re-moderación de ofertas.");
            }

            try
            {
                await Task.Delay(_checkInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        _logger.LogInformation("Servicio de re-moderación diferida de ofertas detenido.");
    }
}
