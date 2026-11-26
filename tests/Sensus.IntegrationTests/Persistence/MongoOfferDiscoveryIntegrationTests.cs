using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.GeoJsonObjectModel;
using Sensus.Domain.Entities;
using Sensus.Domain.Enums;
using Sensus.Domain.Models;
using Sensus.Domain.Services;
using Sensus.Infrastructure.Persistence.Mongo;
using Sensus.Infrastructure.Persistence.Mongo.Documents;
using Sensus.Infrastructure.Persistence.Mongo.Repositories;
using Testcontainers.MongoDb;

namespace Sensus.IntegrationTests.Persistence;

public sealed class MongoOfferDiscoveryContainerFixture : IAsyncLifetime
{
    private readonly string? _envConn = Environment.GetEnvironmentVariable("MONGO_CONNECTION_STRING");
    private MongoDbContainer? _container;

    public string ConnectionString { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        if (!string.IsNullOrWhiteSpace(_envConn))
        {
            ConnectionString = _envConn;
            return;
        }

        for (int attempt = 1; attempt <= 2; attempt++)
        {
            var candidate = new MongoDbBuilder("mongo:7.0").Build();
            try
            {
                await candidate.StartAsync();
                _container = candidate;
                ConnectionString = _container.GetConnectionString();
                return;
            }
            catch when (attempt < 2)
            {
                await candidate.DisposeAsync();
            }
        }
    }

    public async Task DisposeAsync()
    {
        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }
}

/// <summary>
/// Pruebas reales contra MongoDB (usa <c>MONGO_CONNECTION_STRING</c> si está configurada o Testcontainers)
/// para verificar índices españoles, prefijos normalizados, visibilidad y alternativas comparables (Specs 041 y 043).
/// </summary>
public sealed class MongoOfferDiscoveryIntegrationTests(MongoOfferDiscoveryContainerFixture fixture)
    : IClassFixture<MongoOfferDiscoveryContainerFixture>, IAsyncLifetime
{
    private const double RefLat = 19.432608;
    private const double RefLng = -99.133209;
    private static readonly DateTime Now = new(2026, 11, 20, 12, 0, 0, DateTimeKind.Utc);

    private readonly string _databaseName = $"sensus_discovery_{Guid.NewGuid():N}";
    private MongoDbContext _context = null!;
    private MongoOfferRepository _repository = null!;

    public Task InitializeAsync()
    {
        _context = new MongoDbContext(
            Options.Create(new MongoDbSettings { ConnectionString = fixture.ConnectionString, DatabaseName = _databaseName }),
            NullLogger<MongoDbContext>.Instance);
        _repository = new MongoOfferRepository(_context, NullLogger<MongoOfferRepository>.Instance);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        if (!string.IsNullOrWhiteSpace(fixture.ConnectionString))
        {
            await new MongoClient(fixture.ConnectionString).DropDatabaseAsync(_databaseName);
        }
    }

    [Fact]
    public async Task RealMongo_ShouldCreateSpanishTextNormalizedTitleAndAlternativesIndexes()
    {
        List<BsonDocument> indexes = await (await _context.Offers.Indexes.ListAsync()).ToListAsync();

        BsonDocument? spanishText = indexes.FirstOrDefault(i => i["name"] == "txt_offers_spanish");
        spanishText.Should().NotBeNull();
        spanishText!["default_language"].AsString.Should().Be("spanish");

        indexes.Should().Contain(i => i["name"] == "ix_offers_title_normalized");
        indexes.Should().Contain(i => i["name"] == "ix_offers_category_status_price");
    }

    [Fact]
    public async Task RealMongo_HybridSearch_ShouldSupportSpanishStemmingLivePrefixCategoryMatchGuestRadiusAndVisibility()
    {
        Guid activeMerchantId = Guid.NewGuid();
        Guid farMerchantId = Guid.NewGuid();
        Guid inactiveMerchantId = Guid.NewGuid();

        await SeedMerchantAsync(activeMerchantId, "Horno La Espiga", 19.4330, -99.1330, active: true);   // ~50m
        await SeedMerchantAsync(farMerchantId, "Panadería Lejana", 19.4750, -99.1332, active: true);     // ~4.7km
        await SeedMerchantAsync(inactiveMerchantId, "Comercio Inactivo", 19.4328, -99.1331, active: false); // ~25m

        // 1. Oferta visible cercana con acentos (prefijo "pa" / stemming español)
        Offer bag = CreateOffer(activeMerchantId, "Panadería Artesanal", "Conchas y bolillos recién horneados", 100m, 20, 5, 19.4330, -99.1330, OfferCategory.Bakery);
        // 2. Oferta visible lejana (~4.7km, visible a 10km, excluida a 2km para invitados)
        Offer farOffer = CreateOffer(farMerchantId, "Pastel de Chocolate", "Horneado hoy", 200m, 10, 4, 19.4750, -99.1332, OfferCategory.Bakery);
        // 3. Exclusiones obligatorias (FR-041-03, FR-041-06)
        Offer expired = CreateOffer(activeMerchantId, "Pan Expirado", "No visible", 50m, 10, 5, 19.4330, -99.1330, OfferCategory.Bakery, endsAt: Now.AddHours(-1));
        Offer noStock = Offer.Reconstitute(
            Guid.NewGuid(),
            activeMerchantId,
            "Pan Agotado",
            "Sin unidades",
            50m,
            10,
            45m,
            0,
            Now.AddHours(-2),
            Now.AddDays(2),
            OfferStatus.Published,
            null,
            19.4330,
            -99.1330,
            Now.AddHours(-3),
            null,
            0,
            1,
            OfferCategory.Bakery.ToString());
        Offer paused = CreateOffer(activeMerchantId, "Pan Pausado", "Pausada", 50m, 10, 5, 19.4330, -99.1330, OfferCategory.Bakery);
        paused.Pause(Now);
        Offer inactiveMerchantOffer = CreateOffer(inactiveMerchantId, "Pan de Comercio Inactivo", "Oculta", 50m, 10, 5, 19.4328, -99.1331, OfferCategory.Bakery);

        await _repository.AddAsync(bag);
        await _repository.AddAsync(farOffer);
        await _repository.AddAsync(expired);
        await _repository.AddAsync(noStock);
        await _repository.AddAsync(paused);
        await _repository.AddAsync(inactiveMerchantOffer);

        // Prefijo de 2 caracteres ("pa") encuentra títulos normalizados sin acentos usando ix_offers_title_normalized
        IReadOnlyList<OfferSearchCandidate> prefixHits = await _repository.SearchOffersHybridAsync("pa", "pa", RefLat, RefLng, 2000, 100, Now);
        prefixHits.Select(c => c.OfferId).Should().Contain(bag.Id);
        prefixHits.Select(c => c.OfferId).Should().NotContain([farOffer.Id, expired.Id, noStock.Id, paused.Id, inactiveMerchantOffer.Id]);

        // Stemming español con palabra completa ("horneando" -> "horneados") via txt_offers_spanish
        IReadOnlyList<OfferSearchCandidate> stemHits = await _repository.SearchOffersHybridAsync("horneando", "horneando", RefLat, RefLng, 10000, 100, Now);
        stemHits.Select(c => c.OfferId).Should().Contain([bag.Id, farOffer.Id]);
        stemHits.Select(c => c.OfferId).Should().NotContain([expired.Id, noStock.Id, paused.Id, inactiveMerchantOffer.Id]);

        // Búsqueda por nombre de comercio activo ("la espiga")
        IReadOnlyList<OfferSearchCandidate> merchantHits = await _repository.SearchOffersHybridAsync("la espiga", "la espiga", RefLat, RefLng, 5000, 100, Now);
        merchantHits.Select(c => c.OfferId).Should().Contain(bag.Id);

        // Fallback vacío ordena por cercanía
        IReadOnlyList<OfferSearchCandidate> emptyHits = await _repository.SearchOffersHybridAsync(null, null, RefLat, RefLng, 10000, 100, Now);
        IReadOnlyList<RankedOfferSearchEntry> rankedEmpty = OfferSearchRanker.Rank(null, emptyHits, new OfferSearchPolicy(new OfferSearchWeights(), 10000));
        rankedEmpty.Select(e => e.Candidate.OfferId).Should().Equal(bag.Id, farOffer.Id);
    }

    [Fact]
    public async Task RealMongo_Alternatives_ShouldFilterByCategory120PercentFinalPriceAndGuestDistance()
    {
        Guid merchantA = Guid.NewGuid();
        Guid merchantB = Guid.NewGuid();
        Guid merchantInactive = Guid.NewGuid();
        await SeedMerchantAsync(merchantA, "Panadería Origen", 19.4326, -99.1332, active: true);
        await SeedMerchantAsync(merchantB, "Panadería Vecina", 19.4680, -99.1332, active: true); // ~3.9km
        await SeedMerchantAsync(merchantInactive, "Inactiva", 19.4330, -99.1330, active: false);

        // Original: originalPrice=200, discount=50% => finalPrice=100 => 120% tope = 120 (FR-043-04)
        Offer origin = CreateOffer(merchantA, "Rosca Especial", "Deliciosa rosca", 200m, 50, 5, 19.4326, -99.1332, OfferCategory.Bakery);
        Offer nearWithinBudget = CreateOffer(merchantA, "Croissants", "Recién salidos", 150m, 20, 4, 19.4340, -99.1332, OfferCategory.Bakery); // final=120
        Offer farWithinBudget = CreateOffer(merchantB, "Bolillos 4km", "Recién salidos", 100m, 10, 4, 19.4680, -99.1332, OfferCategory.Bakery); // final=90, ~3.9km
        Offer overBudget = CreateOffer(merchantA, "Pastel Caro", "Muy caro", 150m, 10, 4, 19.4330, -99.1332, OfferCategory.Bakery); // final=135 > 120
        Offer otherCategory = CreateOffer(merchantA, "Queso Fresco", "Otra categoría", 80m, 10, 4, 19.4330, -99.1332, OfferCategory.Dairy);
        Offer inactiveOffer = CreateOffer(merchantInactive, "Oculta", "Comercio inactivo", 80m, 10, 4, 19.4330, -99.1330, OfferCategory.Bakery);
        Offer pausedOrigin = CreateOffer(merchantA, "Pausada", "No visible", 100m, 10, 4, 19.4326, -99.1332, OfferCategory.Bakery);
        pausedOrigin.Pause(Now);

        await _repository.AddAsync(origin);
        await _repository.AddAsync(nearWithinBudget);
        await _repository.AddAsync(farWithinBudget);
        await _repository.AddAsync(overBudget);
        await _repository.AddAsync(otherCategory);
        await _repository.AddAsync(inactiveOffer);
        await _repository.AddAsync(pausedOrigin);

        // Registrado (10 km): recibe nearWithinBudget (120 <= 120) y farWithinBudget (~3.9km), excluye origin, overBudget, otherCategory e inactiveOffer
        OfferAlternativesLookupResult regResult = await _repository.GetAlternativesAsync(origin.Id, 10_000, 1.20m, Now);
        regResult.FoundAndVisible.Should().BeTrue();
        regResult.Candidates.Select(c => c.OfferId).Should().Equal(nearWithinBudget.Id, farWithinBudget.Id);

        // Invitado (2 km): excluye farWithinBudget (~3.9km)
        OfferAlternativesLookupResult guestResult = await _repository.GetAlternativesAsync(origin.Id, 2_000, 1.20m, Now);
        guestResult.FoundAndVisible.Should().BeTrue();
        guestResult.Candidates.Select(c => c.OfferId).Should().Equal(nearWithinBudget.Id);

        // Pausada o inexistente -> FoundAndVisible = false idéntico
        (await _repository.GetAlternativesAsync(pausedOrigin.Id, 10_000, 1.20m, Now)).FoundAndVisible.Should().BeFalse();
        (await _repository.GetAlternativesAsync(Guid.NewGuid(), 10_000, 1.20m, Now)).FoundAndVisible.Should().BeFalse();
    }

    private async Task SeedMerchantAsync(Guid merchantId, string name, double lat, double lng, bool active)
    {
        var doc = new ComercioDocument
        {
            Id = merchantId,
            UsuarioId = Guid.NewGuid(),
            NombreComercial = name,
            Direccion = "Calle Centro 1",
            CodigoPostal = "06000",
            Latitud = lat,
            Longitud = lng,
            Ubicacion = GeoJson.Point(GeoJson.Geographic(lng, lat)),
            Activo = active,
            FechaRegistro = Now.AddDays(-10)
        };

        await _context.Comercios.InsertOneAsync(doc);
    }

    private static Offer CreateOffer(
        Guid merchantId,
        string title,
        string description,
        decimal originalPrice,
        int discountPct,
        int stock,
        double lat,
        double lng,
        OfferCategory category,
        DateTime? endsAt = null)
    {
        return Offer.Create(
            merchantId,
            title,
            description,
            originalPrice,
            discountPct,
            stock,
            Now.AddHours(-2),
            endsAt ?? Now.AddDays(3),
            null,
            lat,
            lng,
            Now.AddHours(-3),
            category.ToString(),
            requireValidCategory: true).Value;
    }
}
