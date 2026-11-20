using System.Security.Cryptography;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using NSubstitute;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Common.Settings;
using Sensus.Domain.Entities;
using Sensus.Domain.Enums;
using Sensus.Domain.Repositories;
using Sensus.Infrastructure.Persistence.Mongo;
using Sensus.Infrastructure.Persistence.Mongo.Documents;
using Sensus.Infrastructure.Persistence.Mongo.Repositories;
using Sensus.Infrastructure.Security;
using Sensus.Infrastructure.Services;

namespace Sensus.IntegrationTests.Persistence;

/// <summary>
/// Pruebas de persistencia sobre <c>offer_interactions</c> (Spec 055: CA-055-01..04 e índices TTL/purga/métricas).
/// El round-trip sin PII corre siempre; las pruebas contra MongoDB real corren cuando <c>MONGO_CONNECTION_STRING</c> está definida (CI).
/// </summary>
public sealed class MongoOfferInteractionRepositoryTests : IAsyncLifetime
{
    private static readonly DateTime Now = new(2026, 11, 20, 12, 0, 0, DateTimeKind.Utc);

    private readonly string? _connectionString = Environment.GetEnvironmentVariable("MONGO_CONNECTION_STRING");
    private readonly string _databaseName = $"sensus_interactions_{Guid.NewGuid():N}";
    private MongoDbContext? _context;
    private MongoOfferInteractionRepository? _repository;

    public Task InitializeAsync()
    {
        if (!string.IsNullOrWhiteSpace(_connectionString))
        {
            _context = new MongoDbContext(
                Options.Create(new MongoDbSettings { ConnectionString = _connectionString, DatabaseName = _databaseName }),
                NullLogger<MongoDbContext>.Instance,
                Options.Create(new InteractionSettings { RetentionDays = 180 }));
            _repository = new MongoOfferInteractionRepository(_context);
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

    private bool Enabled => _context is not null && _repository is not null;

    [Fact]
    public void OfferInteractionDocument_RoundTripsWithoutPiiOrCoordinates()
    {
        List<OfferInteractionItem> items = Enumerable.Range(1, 20)
            .Select(i => OfferInteractionItem.Create(Guid.NewGuid(), i, FeedSlot.Ranked, "Bakery", DistanceBucket.Range0To1Km).Value)
            .ToList();

        OfferInteraction entity = OfferInteraction.Create(
            OfferInteractionType.Impression,
            new string('c', 64),
            keyVersion: 2,
            snapshotId: "snap-batch-1",
            rankingVersion: "feed-v1",
            page: 1,
            items,
            Now).Value;

        OfferInteractionDocument document = OfferInteractionDocument.FromDomain(entity);
        BsonDocument bson = document.ToBsonDocument();

        bson.Contains("userId").Should().BeFalse();
        bson.Contains("latitude").Should().BeFalse();
        bson.Contains("longitude").Should().BeFalse();
        bson.Contains("email").Should().BeFalse();
        bson["type"].AsString.Should().Be("impression");
        bson["keyVersion"].AsInt32.Should().Be(2);
        bson["items"].AsBsonArray.Should().HaveCount(20);

        OfferInteraction restored = document.ToDomain();
        restored.Type.Should().Be(OfferInteractionType.Impression);
        restored.PseudoUserId.Should().Be(entity.PseudoUserId);
        restored.SnapshotId.Should().Be("snap-batch-1");
        restored.Items.Should().HaveCount(20);
        restored.Items[0].DistanceBucket.Should().Be(DistanceBucket.Range0To1Km);
    }

    [Fact]
    public async Task RealMongo_ShouldCreateTtlIndexRecordPageAndPurgeMultiVersionUser()
    {
        if (!Enabled)
        {
            return;
        }

        List<BsonDocument> indexes = await (await _context!.OfferInteractions.Indexes.ListAsync()).ToListAsync();
        BsonDocument? ttlIndex = indexes.FirstOrDefault(idx => idx["name"] == "ttl_offer_interactions_occurred_at");
        ttlIndex.Should().NotBeNull();
        ttlIndex!["expireAfterSeconds"].ToInt64().Should().Be(180L * 86400L);
        indexes.Should().Contain(idx => idx["name"] == "ix_offer_interactions_pseudo_key");
        indexes.Should().Contain(idx => idx["name"] == "ix_offer_interactions_quality");

        Guid userId = Guid.NewGuid();
        string key1 = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        string key2 = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

        IDateTimeProvider clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(Now);
        IInteractionMetricsPublisher metrics = Substitute.For<IInteractionMetricsPublisher>();

        var recorderV1 = new MongoOfferInteractionRecorder(
            new HmacUserPseudonymizer(Options.Create(new InteractionSettings
            {
                ActiveKeyVersion = 1,
                HmacKeys = new Dictionary<int, string> { [1] = key1 }
            })),
            _repository!,
            metrics,
            clock,
            Options.Create(new InteractionSettings { Enabled = true }),
            NullLogger<MongoOfferInteractionRecorder>.Instance);

        var recorderV2 = new MongoOfferInteractionRecorder(
            new HmacUserPseudonymizer(Options.Create(new InteractionSettings
            {
                ActiveKeyVersion = 2,
                HmacKeys = new Dictionary<int, string> { [1] = key1, [2] = key2 }
            })),
            _repository!,
            metrics,
            clock,
            Options.Create(new InteractionSettings { Enabled = true }),
            NullLogger<MongoOfferInteractionRecorder>.Instance);

        List<OfferInteractionItem> feedPage = Enumerable.Range(1, 20)
            .Select(i => OfferInteractionItem.Create(Guid.NewGuid(), i, FeedSlot.Ranked, "Grocery", DistanceBucket.Range1To3Km).Value)
            .ToList();

        // CA-055-01: 20 impresiones en 1 documento + 1 open con dos versiones distintas de clave HMAC
        await recorderV1.RecordImpressionsAsync(userId, "snap-1", "feed-v1", 1, feedPage);
        await recorderV2.RecordSingleAsync(userId, OfferInteractionType.Open, feedPage[0], "feed-v1", "snap-1");

        long totalDocs = await _context.OfferInteractions.CountDocumentsAsync(FilterDefinition<OfferInteractionDocument>.Empty);
        totalDocs.Should().Be(2);

        IReadOnlyList<InteractionQualityAggregate> quality = await _repository!.AggregateDailyQualityAsync(Now.AddHours(-1));
        quality.Should().ContainSingle(q => q.RankingVersion == "feed-v1" && q.Slot == FeedSlot.Ranked && q.Impressions == 20 && q.Opens == 1);

        InteractionCollectionStorageSize size = await _repository.GetStorageSizeAsync();
        size.CollectionBytes.Should().BeGreaterThanOrEqualTo(0);
        size.ClusterDataBytes.Should().BeGreaterThanOrEqualTo(0);

        // CA-055-04: purga por usuario elimina eventos de ambas versiones de clave
        await recorderV2.PurgeByUserAsync(userId);
        long remainingDocs = await _context.OfferInteractions.CountDocumentsAsync(FilterDefinition<OfferInteractionDocument>.Empty);
        remainingDocs.Should().Be(0);
    }
}
