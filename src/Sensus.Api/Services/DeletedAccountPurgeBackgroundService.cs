using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Users.Commands.PurgeDeletedAccounts;
using Sensus.Domain.Common;

namespace Sensus.Api.Services;

/// <summary>
/// Job en segundo plano que purga las cuentas dadas de baja cuya retención venció (Spec 060,
/// FR-060-04 y NFR-060-03). La retención y la frecuencia se leen de <c>AccountDeletion</c>.
/// </summary>
public sealed class DeletedAccountPurgeBackgroundService : BackgroundService
{
    public const int DefaultRetentionDays = 30;
    public const int DefaultPurgeIntervalHours = 24;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly IDateTimeProvider _clock;
    private readonly ILogger<DeletedAccountPurgeBackgroundService> _logger;
    private readonly int _retentionDays;
    private readonly TimeSpan _checkInterval;

    public DeletedAccountPurgeBackgroundService(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        IDateTimeProvider clock,
        ILogger<DeletedAccountPurgeBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _clock = clock;
        _logger = logger;
        _retentionDays = Math.Max(1, configuration.GetValue("AccountDeletion:RetentionDays", DefaultRetentionDays));
        _checkInterval = TimeSpan.FromHours(Math.Max(
            1,
            configuration.GetValue("AccountDeletion:PurgeIntervalHours", DefaultPurgeIntervalHours)));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Servicio de purga de cuentas iniciado. Retención: {Days} días. Intervalo: {Interval}",
            _retentionDays,
            _checkInterval);

        try
        {
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
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
                DateTime cutoffUtc = DateTime.SpecifyKind(_clock.UtcNow.AddDays(-_retentionDays), DateTimeKind.Utc);
                Result<PurgeDeletedAccountsResult> result =
                    await sender.Send(new PurgeDeletedAccountsCommand(cutoffUtc), stoppingToken);

                if (result.IsFailure)
                {
                    _logger.LogWarning("La purga de cuentas dadas de baja falló: {Error}", result.Error.Message);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error durante la ejecución del job de purga de cuentas.");
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

        _logger.LogInformation("Servicio de purga de cuentas detenido.");
    }
}
