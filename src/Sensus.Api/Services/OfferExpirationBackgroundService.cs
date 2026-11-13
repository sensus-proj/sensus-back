using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Sensus.Application.Offers.Commands.ExpireOffers;

namespace Sensus.Api.Services;

/// <summary>
/// Hosted Service en segundo plano que ejecuta el control automático de vigencia periódicamente (SE-022 a SE-025).
/// En entorno de contenedor/Kestrel ejecuta la revisión recurrente; en AWS Lambda se complementa con EventBridge Scheduler invocando el endpoint de revisión.
/// </summary>
public sealed class OfferExpirationBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OfferExpirationBackgroundService> _logger;
    private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(5);

    public OfferExpirationBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<OfferExpirationBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Servicio en segundo plano de expiración de ofertas iniciado. Intervalo: {Interval}", _checkInterval);

        // Esperar unos segundos al inicio para permitir que la aplicación arranque completamente
        await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var sender = scope.ServiceProvider.GetRequiredService<ISender>();

                var result = await sender.Send(new ExpireOffersCommand(), stoppingToken);
                if (result.IsSuccess && result.Value.ExpiredCount > 0)
                {
                    _logger.LogInformation("Expiración automática: {Count} ofertas pasaron a estado Expired.", result.Value.ExpiredCount);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error durante la ejecución del job de expiración de ofertas.");
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

        _logger.LogInformation("Servicio en segundo plano de expiración de ofertas detenido.");
    }
}
