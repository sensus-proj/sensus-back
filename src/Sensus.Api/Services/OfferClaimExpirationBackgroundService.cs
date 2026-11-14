using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Sensus.Application.Offers.Commands.ExpireClaims;
using Sensus.Application.Offers.DTOs;
using Sensus.Domain.Common;

namespace Sensus.Api.Services;

/// <summary>
/// Job en segundo plano que expira los apartados vencidos y libera el stock reservado
/// (FR-033-05, NFR-033-03). En AWS Lambda se complementa con EventBridge.
/// </summary>
public sealed class OfferClaimExpirationBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OfferClaimExpirationBackgroundService> _logger;
    private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(5);

    public OfferClaimExpirationBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<OfferClaimExpirationBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Servicio en segundo plano de expiración de apartados iniciado. Intervalo: {Interval}",
            _checkInterval);

        await Task.Delay(TimeSpan.FromSeconds(20), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using IServiceScope scope = _scopeFactory.CreateScope();
                ISender sender = scope.ServiceProvider.GetRequiredService<ISender>();

                Result<ExpireClaimsResultDto> result = await sender.Send(new ExpireClaimsCommand(), stoppingToken);
                if (result.IsSuccess && result.Value.ExpiredCount > 0)
                {
                    _logger.LogInformation(
                        "Expiración de apartados: {Count} vencidos y {Units} unidades devueltas.",
                        result.Value.ExpiredCount,
                        result.Value.ReturnedUnits);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error durante la ejecución del job de expiración de apartados.");
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

        _logger.LogInformation("Servicio en segundo plano de expiración de apartados detenido.");
    }
}
