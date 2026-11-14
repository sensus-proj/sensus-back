using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Sensus.Application.Media.Commands.PurgeQuarantinedMedia;
using Sensus.Domain.Common;

namespace Sensus.Api.Services;

/// <summary>
/// Job en segundo plano que elimina el contenido en cuarentena que superó la retención máxima
/// (FR-019-03, NFR-019-01). La retención se configura en <c>Moderation:QuarantineRetentionDays</c> (30 por defecto).
/// </summary>
public sealed class QuarantineRetentionBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<QuarantineRetentionBackgroundService> _logger;
    private readonly TimeSpan _checkInterval = TimeSpan.FromHours(6);

    public QuarantineRetentionBackgroundService(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<QuarantineRetentionBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        int retentionDays = _configuration.GetValue("Moderation:QuarantineRetentionDays", PurgeQuarantinedMediaCommand.DefaultRetentionDays);
        _logger.LogInformation(
            "Servicio de retención de cuarentena iniciado. Retención: {Days} días. Intervalo: {Interval}",
            retentionDays,
            _checkInterval);

        try
        {
            await Task.Delay(TimeSpan.FromSeconds(45), stoppingToken);
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
                Result<PurgeQuarantinedMediaResultDto> result =
                    await sender.Send(new PurgeQuarantinedMediaCommand(retentionDays), stoppingToken);

                if (result.IsFailure)
                {
                    _logger.LogWarning("La purga de cuarentena falló: {Error}", result.Error.Message);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error durante la ejecución del job de retención de cuarentena.");
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

        _logger.LogInformation("Servicio de retención de cuarentena detenido.");
    }
}
