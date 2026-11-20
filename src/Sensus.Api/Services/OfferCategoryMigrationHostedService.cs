using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Sensus.Infrastructure.Migration;

namespace Sensus.Api.Services;

/// <summary>
/// Ejecuta una sola vez, al arrancar la API, la migración de categorías legadas de ofertas (Spec 034, FR-034-09,
/// T034-15). La migración es idempotente: con el marcador <c>offer_category_v1</c> presente solo hace una lectura.
/// Un fallo se registra sin PII y no impide el arranque; se reintenta en el siguiente arranque.
/// </summary>
public sealed class OfferCategoryMigrationHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OfferCategoryMigrationHostedService> _logger;

    public OfferCategoryMigrationHostedService(
        IServiceScopeFactory scopeFactory,
        ILogger<OfferCategoryMigrationHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            using IServiceScope scope = _scopeFactory.CreateScope();
            IOfferCategoryMigration migration = scope.ServiceProvider.GetRequiredService<IOfferCategoryMigration>();
            OfferCategoryMigrationResult result = await migration.ExecuteAsync(stoppingToken);

            _logger.LogInformation(
                "Migración de categorías de ofertas al arrancar. YaEjecutada={AlreadyExecuted}, Escaneadas={TotalScanned}, Convertidas={ConvertedCount}, Anuladas={NulledCount}.",
                result.AlreadyExecuted,
                result.TotalScanned,
                result.ConvertedCount,
                result.NulledCount);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Apagado de la instancia durante la migración: se reintentará en el siguiente arranque.
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Falló la migración de categorías de ofertas al arrancar. Se reintentará en el siguiente arranque.");
        }
    }
}
