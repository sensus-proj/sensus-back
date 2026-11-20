using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Sensus.Domain.Entities;
using Sensus.Domain.Enums;
using Sensus.Infrastructure.Migration;
using Sensus.Infrastructure.Persistence.Mongo;
using Sensus.Infrastructure.Persistence.Mongo.Documents;
using Sensus.TestKit.Fakes;

namespace Sensus.IntegrationTests.Migration;

/// <summary>
/// Pruebas de normalización y migración única e idempotente de categorías de ofertas (Spec 034, FR-034-09, T034-14).
/// Las pruebas de base de datos real corren contra MongoDB cuando <c>MONGO_CONNECTION_STRING</c> está definida (CI).
/// </summary>
public sealed class OfferCategoryMigrationServiceTests : IAsyncLifetime
{
    private static readonly DateTime Now = new(2026, 11, 20, 10, 0, 0, DateTimeKind.Utc);

    private readonly string? _connectionString = Environment.GetEnvironmentVariable("MONGO_CONNECTION_STRING");
    private readonly string _databaseName = $"sensus_offer_cat_mig_{Guid.NewGuid():N}";
    private MongoDbContext? _context;
    private OfferCategoryMigrationService? _service;

    public Task InitializeAsync()
    {
        if (!string.IsNullOrWhiteSpace(_connectionString))
        {
            _context = new MongoDbContext(
                Options.Create(new MongoDbSettings { ConnectionString = _connectionString, DatabaseName = _databaseName }),
                NullLogger<MongoDbContext>.Instance);
            var clock = new FakeDateTimeProvider();
            clock.Set(Now);
            _service = new OfferCategoryMigrationService(
                _context,
                clock,
                NullLogger<OfferCategoryMigrationService>.Instance);
        }

        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        if (!string.IsNullOrWhiteSpace(_connectionString))
        {
            await new MongoClient(_connectionString).DropDatabaseAsync(_databaseName);
        }
    }

    private bool Enabled => _context is not null && _service is not null;

    [Theory]
    [InlineData("Abarrotes", "Grocery", nameof(OfferCategoryMigrationService.CategoryResolutionKind.Converted))]
    [InlineData("Limpieza y Hogar", "CleaningHome", nameof(OfferCategoryMigrationService.CategoryResolutionKind.Converted))]
    [InlineData("Perecederos", "Perishables", nameof(OfferCategoryMigrationService.CategoryResolutionKind.Converted))]
    [InlineData("Lácteos", "Dairy", nameof(OfferCategoryMigrationService.CategoryResolutionKind.Converted))]
    [InlineData("Comida", "Food", nameof(OfferCategoryMigrationService.CategoryResolutionKind.Converted))]
    [InlineData("Panadería", "Bakery", nameof(OfferCategoryMigrationService.CategoryResolutionKind.Converted))]
    [InlineData("Bebidas", "Beverages", nameof(OfferCategoryMigrationService.CategoryResolutionKind.Converted))]
    [InlineData("Mascotas", "Pets", nameof(OfferCategoryMigrationService.CategoryResolutionKind.Converted))]
    [InlineData("Farmacia", "Pharmacy", nameof(OfferCategoryMigrationService.CategoryResolutionKind.Converted))]
    [InlineData("bakery", "Bakery", nameof(OfferCategoryMigrationService.CategoryResolutionKind.Converted))]
    [InlineData("Bakery", "Bakery", nameof(OfferCategoryMigrationService.CategoryResolutionKind.AlreadyCanonical))]
    [InlineData("Moda", null, nameof(OfferCategoryMigrationService.CategoryResolutionKind.Nulled))]
    [InlineData("Todo", null, nameof(OfferCategoryMigrationService.CategoryResolutionKind.Nulled))]
    [InlineData("", null, nameof(OfferCategoryMigrationService.CategoryResolutionKind.Nulled))]
    [InlineData(null, null, nameof(OfferCategoryMigrationService.CategoryResolutionKind.AlreadyNull))]
    public void ClassifyCategory_ShouldResolveAllLegacyCanonicalAndUnknownValues(
        string? raw,
        string? expectedTarget,
        string expectedKind)
    {
        (string? target, OfferCategoryMigrationService.CategoryResolutionKind kind) =
            OfferCategoryMigrationService.ClassifyCategory(raw);

        target.Should().Be(expectedTarget);
        kind.ToString().Should().Be(expectedKind);
    }

    [Fact]
    public async Task ExecuteAsync_InRealMongo_ShouldMigrateOnceWithExactEvidenceAndBeIdempotent()
    {
        if (!Enabled)
        {
            return;
        }

        Guid merchantId = Guid.NewGuid();
        string?[] categories =
        [
            "Abarrotes",
            "Limpieza y Hogar",
            "Perecederos",
            "Lácteos",
            "Comida",
            "Panadería",
            "Bebidas",
            "Mascotas",
            "Farmacia",
            "Bakery",
            "Moda",
            null
        ];

        var docs = categories.Select((cat, idx) =>
        {
            Offer offer = Offer.Create(
                merchantId,
                $"Oferta {idx}",
                "Descripción de prueba suficientemente larga",
                100m,
                20,
                5,
                Now.AddMinutes(5),
                Now.AddDays(1),
                nowUtc: Now,
                category: cat).Value;
            return OfferDocument.FromDomain(offer);
        }).ToList();

        await _context!.Offers.InsertManyAsync(docs);

        OfferCategoryMigrationResult firstRun = await _service!.ExecuteAsync();

        firstRun.AlreadyExecuted.Should().BeFalse();
        firstRun.TotalScanned.Should().Be(12);
        firstRun.ConvertedCount.Should().Be(9);
        firstRun.AlreadyCanonicalCount.Should().Be(1);
        firstRun.NulledCount.Should().Be(1);
        firstRun.AlreadyNullCount.Should().Be(1);

        List<OfferDocument> persisted = await _context.Offers.Find(FilterDefinition<OfferDocument>.Empty).ToListAsync();
        persisted.Select(d => d.Category).Should().BeEquivalentTo(
        [
            "Grocery",
            "CleaningHome",
            "Perishables",
            "Dairy",
            "Food",
            "Bakery",
            "Beverages",
            "Pets",
            "Pharmacy",
            "Bakery",
            null,
            null
        ]);

        SchemaMigrationDocument? marker = await _context.SchemaMigrations
            .Find(Builders<SchemaMigrationDocument>.Filter.Eq(m => m.Id, OfferCategoryMigrationService.MigrationId))
            .FirstOrDefaultAsync();
        marker.Should().NotBeNull();
        marker!.ConvertedCount.Should().Be(9);
        marker.NulledCount.Should().Be(1);

        OfferCategoryMigrationResult secondRun = await _service.ExecuteAsync();
        secondRun.AlreadyExecuted.Should().BeTrue();
        secondRun.TotalScanned.Should().Be(12);
        secondRun.ConvertedCount.Should().Be(9);
        secondRun.NulledCount.Should().Be(1);
    }
}
