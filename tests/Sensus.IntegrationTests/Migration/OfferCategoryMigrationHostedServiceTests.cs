using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Sensus.Api.Services;
using Sensus.Infrastructure.Migration;

namespace Sensus.IntegrationTests.Migration;

/// <summary>
/// Ejecución de la migración de categorías al arrancar la API (Spec 034, FR-034-09, T034-15).
/// </summary>
public sealed class OfferCategoryMigrationHostedServiceTests
{
    private static OfferCategoryMigrationHostedService CreateService(IOfferCategoryMigration migration)
    {
        ServiceProvider provider = new ServiceCollection()
            .AddScoped(_ => migration)
            .BuildServiceProvider();

        return new OfferCategoryMigrationHostedService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<OfferCategoryMigrationHostedService>.Instance);
    }

    [Fact]
    public async Task StartAsync_ShouldRunMigrationOnceAtStartup()
    {
        var migration = Substitute.For<IOfferCategoryMigration>();
        migration.ExecuteAsync(Arg.Any<CancellationToken>())
            .Returns(new OfferCategoryMigrationResult(12, 9, 1, 1, 1, AlreadyExecuted: false, DateTime.UtcNow));
        using OfferCategoryMigrationHostedService service = CreateService(migration);

        await service.StartAsync(CancellationToken.None);
        await service.ExecuteTask!;

        await migration.Received(1).ExecuteAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StartAsync_WhenMigrationAlreadyExecuted_ShouldCompleteWithoutErrors()
    {
        var migration = Substitute.For<IOfferCategoryMigration>();
        migration.ExecuteAsync(Arg.Any<CancellationToken>())
            .Returns(new OfferCategoryMigrationResult(12, 9, 1, 1, 1, AlreadyExecuted: true, DateTime.UtcNow));
        using OfferCategoryMigrationHostedService service = CreateService(migration);

        await service.StartAsync(CancellationToken.None);
        Func<Task> waitForCompletion = () => service.ExecuteTask!;

        await waitForCompletion.Should().NotThrowAsync();
        service.ExecuteTask!.IsCompletedSuccessfully.Should().BeTrue();
    }

    [Fact]
    public async Task StartAsync_WhenMigrationFails_ShouldNotStopTheApi()
    {
        var migration = Substitute.For<IOfferCategoryMigration>();
        migration.ExecuteAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("La conexión a MongoDB Atlas no está configurada."));
        using OfferCategoryMigrationHostedService service = CreateService(migration);

        await service.StartAsync(CancellationToken.None);
        Func<Task> waitForCompletion = () => service.ExecuteTask!;

        await waitForCompletion.Should().NotThrowAsync("un fallo de la migración no debe impedir que la API arranque");
        service.ExecuteTask!.IsCompletedSuccessfully.Should().BeTrue();
    }
}
