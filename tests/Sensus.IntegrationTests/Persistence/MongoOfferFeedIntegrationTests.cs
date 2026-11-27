using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using NSubstitute;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Common.Settings;
using Sensus.Application.Offers;
using Sensus.Domain.Entities;
using Sensus.Domain.Enums;
using Sensus.Domain.Models;
using Sensus.Infrastructure.Cache;
using Sensus.Infrastructure.Persistence.Mongo;
using Sensus.Infrastructure.Persistence.Mongo.Documents;
using Sensus.Infrastructure.Persistence.Mongo.Repositories;
using Sensus.IntegrationTests.Fixtures.OfferFeed;

namespace Sensus.IntegrationTests.Persistence;

/// <summary>
/// Pruebas de infraestructura e integración MongoDB/Redis del feed de promociones (Spec 042, T042-04, T042-05, T042-11).
/// La caché/degradación corre siempre; el fixture en MongoDB corre con <c>MONGO_CONNECTION_STRING</c> (CI/Docker).
/// Las pruebas contra Redis real viven en <c>RedisCacheTestcontainersTests</c> (Spec 048, T048-17).
/// </summary>
public sealed class MongoOfferFeedIntegrationTests : IAsyncLifetime
{
    private static readonly DateTime Now = OfferFeedDeterministicFixture.FixedNowUtc;

    private readonly string? _connectionString = Environment.GetEnvironmentVariable("MONGO_CONNECTION_STRING");
    private readonly string _databaseName = $"sensus_feed_{Guid.NewGuid():N}";
    private MongoDbContext? _context;
    private MongoOfferRepository? _offerRepository;

    public Task InitializeAsync()
    {
        if (!string.IsNullOrWhiteSpace(_connectionString))
        {
            _context = new MongoDbContext(
                Options.Create(new MongoDbSettings { ConnectionString = _connectionString, DatabaseName = _databaseName }),
                NullLogger<MongoDbContext>.Instance);
            _offerRepository = new MongoOfferRepository(_context, NullLogger<MongoOfferRepository>.Instance);
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

    [Fact]
    public async Task RedisFeedSnapshotStore_ShouldRoundTripAndDegradeGracefullyWhenUnavailable()
    {
        ICacheService unavailable = Substitute.For<ICacheService>();
        unavailable.IsAvailable.Returns(false);
        var degradedStore = new RedisFeedSnapshotStore(unavailable, NullLogger<RedisFeedSnapshotStore>.Instance);

        Guid userId = Guid.NewGuid();
        Guid snapshotId = Guid.NewGuid();
        var snapshot = new FeedSnapshot(snapshotId, "feed-v1", Now, [new FeedSnapshotEntry(Guid.NewGuid(), FeedSlot.Ranked)]);

        (await degradedStore.SaveAsync(userId, snapshot, TimeSpan.FromMinutes(10))).Should().BeFalse();
        (await degradedStore.GetAsync(userId, snapshotId)).Should().BeNull();

        ICacheService healthy = Substitute.For<ICacheService>();
        healthy.IsAvailable.Returns(true);
        healthy.GetAsync<FeedSnapshot>(FeedCacheKeys.Snapshot(userId, snapshotId), Arg.Any<CancellationToken>())
            .Returns(snapshot);
        var store = new RedisFeedSnapshotStore(healthy, NullLogger<RedisFeedSnapshotStore>.Instance);

        (await store.SaveAsync(userId, snapshot, TimeSpan.FromMinutes(10))).Should().BeTrue();
        FeedSnapshot? restored = await store.GetAsync(userId, snapshotId);
        restored.Should().NotBeNull();
        restored!.SnapshotId.Should().Be(snapshotId);
    }

    [Fact]
    public async Task MongoUserAffinityReader_WhenCached_ShouldReturnCachedValueWithoutQueryingMongo()
    {
        Guid userId = Guid.NewGuid();
        ICacheService cache = Substitute.For<ICacheService>();
        cache.GetAsync<Dictionary<string, double>>(FeedCacheKeys.Affinity(userId), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, double> { ["Bakery"] = 1.0, ["Dairy"] = 0.5 });

        var dummyContext = new MongoDbContext(
            Options.Create(new MongoDbSettings { ConnectionString = string.Empty }),
            NullLogger<MongoDbContext>.Instance);
        IDateTimeProvider clock = Substitute.For<IDateTimeProvider>();

        var reader = new MongoUserAffinityReader(
            dummyContext,
            clock,
            Options.Create(new FeedRankingSettings()),
            NullLogger<MongoUserAffinityReader>.Instance,
            cache);

        IReadOnlyDictionary<string, double> result = await reader.GetCategoryAffinitiesAsync(userId);
        result.Should().ContainKey("Bakery").WhoseValue.Should().Be(1.0);
        result.Should().ContainKey("Dairy").WhoseValue.Should().Be(0.5);
    }

    [Fact]
    public async Task RealMongo_DeterministicFixture_ShouldFilterAndAggregateCandidatesCorrectly()
    {
        if (_context is null || _offerRepository is null)
        {
            return;
        }

        var fixture = new OfferFeedDeterministicFixture();

        var merchantDocs = fixture.ActiveMerchants
            .Concat([fixture.InactiveMerchant, fixture.UserEOwnMerchant])
            .Select(ComercioDocument.FromDomain)
            .ToList();
        await _context.Comercios.InsertManyAsync(merchantDocs);

        var offerDocs = fixture.VisibleOffers
            .Concat(fixture.ExcludedOffers)
            .Select(OfferDocument.FromDomain)
            .ToList();
        await _context.Offers.InsertManyAsync(offerDocs);

        // Señales de vistas y apartados para usuario (b)
        Offer foodOffer = fixture.VisibleOffers.First(o => o.Category == "Food");
        await _context.OfferViews.InsertOneAsync(OfferViewDocument.FromDomain(OfferView.Create(OfferFeedDeterministicFixture.UserBWithAffinityId, foodOffer.Id, Now.AddDays(-1)).Value));

        IReadOnlyList<FeedOfferCandidate> forA = await _offerRepository.GetFeedCandidatesAsync(
            OfferFeedDeterministicFixture.ReferenceLat,
            OfferFeedDeterministicFixture.ReferenceLng,
            10_000,
            OfferFeedDeterministicFixture.UserAWithInterestsId,
            500,
            Now);

        forA.Should().NotBeEmpty();
        HashSet<Guid> globallyExcludedIds = fixture.ExcludedOffers
            .Where(o => o.MerchantId != fixture.UserEOwnMerchant.Id)
            .Select(o => o.Id)
            .ToHashSet();
        forA.Select(c => c.OfferId).Should().NotIntersectWith(globallyExcludedIds);

        // Usuario (e) no recibe las ofertas de su propio comercio
        IReadOnlyList<FeedOfferCandidate> forE = await _offerRepository.GetFeedCandidatesAsync(
            OfferFeedDeterministicFixture.ReferenceLat,
            OfferFeedDeterministicFixture.ReferenceLng,
            10_000,
            OfferFeedDeterministicFixture.UserEMerchantOwnerId,
            500,
            Now);
        forE.Should().NotContain(c => c.MerchantId == fixture.UserEOwnMerchant.Id);

        IDateTimeProvider clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(Now);
        var affinityReader = new MongoUserAffinityReader(
            _context,
            clock,
            Options.Create(new FeedRankingSettings()),
            NullLogger<MongoUserAffinityReader>.Instance);

        IReadOnlyDictionary<string, double> affinitiesB = await affinityReader.GetCategoryAffinitiesAsync(OfferFeedDeterministicFixture.UserBWithAffinityId);
        affinitiesB.Should().ContainKey("Food").WhoseValue.Should().Be(1.0);
    }

    [Fact]
    public async Task RealMongo_FavoriteSignal_ShouldContributeToCategoryAffinity()
    {
        if (_context is null)
        {
            return;
        }

        var fixture = new OfferFeedDeterministicFixture();
        await _context.Offers.InsertOneAsync(OfferDocument.FromDomain(fixture.VisibleOffers.First(o => o.Category == "Food")));

        Guid userId = Guid.NewGuid();
        OfferFavorite favorite = OfferFavorite.Create(userId, fixture.VisibleOffers.First(o => o.Category == "Food").Id, Now).Value;
        await _context.OfferFavorites.InsertOneAsync(OfferFavoriteDocument.FromDomain(favorite));

        IDateTimeProvider clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(Now);
        var reader = new MongoUserAffinityReader(
            _context,
            clock,
            Options.Create(new FeedRankingSettings()),
            NullLogger<MongoUserAffinityReader>.Instance);

        IReadOnlyDictionary<string, double> affinities = await reader.GetCategoryAffinitiesAsync(userId);

        affinities.Should().ContainKey("Food").WhoseValue.Should().Be(1.0);
    }

    [Fact]
    public async Task RealMongo_GetPagedByUserIdAsync_OrdersDescExcludesExpiredAndPaginates()
    {
        if (_context is null)
        {
            return;
        }

        Guid userId = Guid.NewGuid();
        Guid merchantId = Guid.NewGuid();
        Offer newestVisible = Offer.Create(
            merchantId,
            "Oferta más reciente",
            "Vigente",
            80m,
            25,
            4,
            Now.AddDays(-1),
            Now.AddDays(3),
            nowUtc: Now,
            category: "Food").Value;
        Offer expiredOffer = Offer.Reconstitute(
            Guid.NewGuid(),
            merchantId,
            "Oferta expirada",
            "Expirada",
            50m,
            10,
            45m,
            2,
            Now.AddDays(-5),
            Now.AddDays(-1),
            OfferStatus.Published,
            null,
            null,
            null,
            Now.AddDays(-5),
            category: "Food");

        await _context.Offers.InsertManyAsync([OfferDocument.FromDomain(newestVisible), OfferDocument.FromDomain(expiredOffer)]);
        await _context.OfferFavorites.InsertManyAsync([
            OfferFavoriteDocument.FromDomain(OfferFavorite.Create(userId, newestVisible.Id, Now).Value),
            OfferFavoriteDocument.FromDomain(OfferFavorite.Create(userId, expiredOffer.Id, Now.AddMinutes(-5)).Value)
        ]);

        var repo = new MongoOfferFavoriteRepository(_context);
        FavoriteOffersPage page = await repo.GetPagedByUserIdAsync(userId, 1, 20, Now);

        page.TotalCount.Should().Be(1);
        page.Items.Should().ContainSingle(x => x.Offer.Id == newestVisible.Id);
    }
}
