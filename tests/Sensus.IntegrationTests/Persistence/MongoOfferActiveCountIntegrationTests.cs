using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using Sensus.Domain.Entities;
using Sensus.Domain.Enums;
using Sensus.Infrastructure.Persistence.Mongo;
using Sensus.Infrastructure.Persistence.Mongo.Repositories;
using Testcontainers.MongoDb;

namespace Sensus.IntegrationTests.Persistence;

/// <summary>
/// Pruebas reales contra MongoDB (Testcontainers) para verificar <c>ix_offers_merchant_status_ends_at</c>
/// y el conteo exacto de ofertas activas por comercio (Spec 034, FR-034-05).
/// </summary>
public sealed class MongoOfferActiveCountIntegrationTests : IAsyncLifetime
{
    private static readonly DateTime Now = new(2026, 11, 26, 12, 0, 0, DateTimeKind.Utc);

    private readonly string? _envConn = Environment.GetEnvironmentVariable("MONGO_CONNECTION_STRING");
    private readonly string _databaseName = $"sensus_active_count_{Guid.NewGuid():N}";
    private MongoDbContainer? _container;
    private string _connectionString = string.Empty;
    private MongoDbContext _context = null!;
    private MongoOfferRepository _repository = null!;

    public async Task InitializeAsync()
    {
        if (!string.IsNullOrWhiteSpace(_envConn))
        {
            _connectionString = _envConn;
        }
        else
        {
            _container = new MongoDbBuilder("mongo:7.0").Build();
            await _container.StartAsync();
            _connectionString = _container.GetConnectionString();
        }

        _context = new MongoDbContext(
            Options.Create(new MongoDbSettings { ConnectionString = _connectionString, DatabaseName = _databaseName }),
            NullLogger<MongoDbContext>.Instance);
        _repository = new MongoOfferRepository(_context, NullLogger<MongoOfferRepository>.Instance);
    }

    public async Task DisposeAsync()
    {
        if (!string.IsNullOrWhiteSpace(_envConn))
        {
            await new MongoClient(_connectionString).DropDatabaseAsync(_databaseName);
        }

        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }

    [Fact]
    public async Task CountActiveByMerchantAsync_ShouldCreateCompoundIndexAndCountOnlyPublishedNonExpired()
    {
        List<BsonDocument> indexes = await (await _context.Offers.Indexes.ListAsync()).ToListAsync();
        indexes.Should().Contain(i => i["name"] == "ix_offers_merchant_status_ends_at");

        Guid merchantId = Guid.NewGuid();
        Guid otherMerchantId = Guid.NewGuid();

        // 2 ofertas activas (Published con EndsAt > Now)
        await _repository.AddAsync(CreateOffer(merchantId, "Activa 1", OfferStatus.Published, Now.AddHours(2)));
        await _repository.AddAsync(CreateOffer(merchantId, "Activa 2", OfferStatus.Published, Now.AddDays(1)));

        // No deben contarse: Draft, Paused, Expired, Archived, Published ya vencida, otro comercio
        await _repository.AddAsync(CreateOffer(merchantId, "Borrador", OfferStatus.Draft, Now.AddDays(1)));
        await _repository.AddAsync(CreateOffer(merchantId, "Pausada", OfferStatus.Paused, Now.AddDays(1)));
        await _repository.AddAsync(CreateOffer(merchantId, "Expirada", OfferStatus.Expired, Now.AddDays(-1)));
        await _repository.AddAsync(CreateOffer(merchantId, "Archivada", OfferStatus.Archived, Now.AddDays(1)));
        await _repository.AddAsync(CreateOffer(merchantId, "VencidaPublished", OfferStatus.Published, Now.AddSeconds(-1)));
        await _repository.AddAsync(CreateOffer(otherMerchantId, "Otro comercio", OfferStatus.Published, Now.AddDays(1)));

        long activeCount = await _repository.CountActiveByMerchantAsync(merchantId, Now);

        activeCount.Should().Be(2);
    }

    [Fact]
    public async Task GetPendingModerationDueAsync_UsesIndexAndReturnsOnlyDuePendingOffers()
    {
        List<BsonDocument> indexes = await (await _context.Offers.Indexes.ListAsync()).ToListAsync();
        indexes.Should().Contain(i => i["name"] == "ix_offers_pending_moderation_retry");

        Guid merchantId = Guid.NewGuid();
        Offer dueEarly = Offer.Reconstitute(
            Guid.NewGuid(), merchantId, "Pendiente 1", "Descripción", 100m, 10, 90m, 5,
            Now.AddHours(-1), Now.AddDays(2), OfferStatus.PendingModeration, null, 19.43, -99.13, Now.AddHours(-1),
            nextModerationRetryAtUtc: Now.AddMinutes(-5));
        Offer dueNow = Offer.Reconstitute(
            Guid.NewGuid(), merchantId, "Pendiente 2", "Descripción", 100m, 10, 90m, 5,
            Now.AddHours(-1), Now.AddDays(2), OfferStatus.PendingModeration, null, 19.43, -99.13, Now.AddHours(-1),
            nextModerationRetryAtUtc: Now);
        Offer notDueYet = Offer.Reconstitute(
            Guid.NewGuid(), merchantId, "Pendiente futura", "Descripción", 100m, 10, 90m, 5,
            Now.AddHours(-1), Now.AddDays(2), OfferStatus.PendingModeration, null, 19.43, -99.13, Now.AddHours(-1),
            nextModerationRetryAtUtc: Now.AddMinutes(5));
        Offer published = CreateOffer(merchantId, "Publicada", OfferStatus.Published, Now.AddDays(2));

        await _repository.AddAsync(dueNow);
        await _repository.AddAsync(dueEarly);
        await _repository.AddAsync(notDueYet);
        await _repository.AddAsync(published);

        IReadOnlyList<Offer> due = await _repository.GetPendingModerationDueAsync(Now, 10);

        due.Select(o => o.Id).Should().Equal(dueEarly.Id, dueNow.Id);
    }

    private static Offer CreateOffer(Guid merchantId, string title, OfferStatus status, DateTime endsAt) =>
        Offer.Reconstitute(
            Guid.NewGuid(),
            merchantId,
            title,
            "Descripción válida de prueba",
            100m,
            20,
            80m,
            5,
            Now.AddDays(-2),
            endsAt,
            status,
            null,
            19.43,
            -99.13,
            Now.AddDays(-2));
}
