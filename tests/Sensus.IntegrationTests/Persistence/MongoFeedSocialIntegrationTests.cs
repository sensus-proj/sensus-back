using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using Sensus.Domain.Entities;
using Sensus.Infrastructure.Persistence.Mongo;
using Sensus.Infrastructure.Persistence.Mongo.Repositories;
using Testcontainers.MongoDb;

namespace Sensus.IntegrationTests.Persistence;

public sealed class MongoFeedSocialContainerFixture : IAsyncLifetime
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
/// Pruebas contra MongoDB real (usa <c>MONGO_CONNECTION_STRING</c> si está configurada o Testcontainers)
/// para la Spec 039 (unicidad y concurrencia de me gusta) y la Spec 026 (feed cercano: filtro, orden e índice).
/// </summary>
public sealed class MongoFeedSocialIntegrationTests(MongoFeedSocialContainerFixture fixture)
    : IClassFixture<MongoFeedSocialContainerFixture>, IAsyncLifetime
{
    private const double Lat = -12.05;
    private const double Lng = -77.04;
    private static readonly DateTime Now = new(2026, 11, 25, 12, 0, 0, DateTimeKind.Utc);

    private readonly string _databaseName = $"sensus_feed_social_{Guid.NewGuid():N}";
    private MongoDbContext _context = null!;
    private MongoFeedPostRepository _posts = null!;
    private MongoFeedPostLikeRepository _likes = null!;

    public Task InitializeAsync()
    {
        _context = new MongoDbContext(
            Options.Create(new MongoDbSettings { ConnectionString = fixture.ConnectionString, DatabaseName = _databaseName }),
            NullLogger<MongoDbContext>.Instance);
        _posts = new MongoFeedPostRepository(_context);
        _likes = new MongoFeedPostLikeRepository(_context);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        if (!string.IsNullOrWhiteSpace(fixture.ConnectionString))
        {
            await new MongoClient(fixture.ConnectionString).DropDatabaseAsync(_databaseName);
        }
    }

    // ---------------- Spec 039 ----------------

    [Fact]
    public async Task Likes_UniqueIndexExists()
    {
        List<BsonDocument> indexes = await (await _context.FeedPostLikes.Indexes.ListAsync()).ToListAsync();

        BsonDocument? unique = indexes.FirstOrDefault(i => i["name"] == "ux_feed_post_likes_post_user");
        unique.Should().NotBeNull();
        unique!["unique"].AsBoolean.Should().BeTrue();
    }

    [Fact]
    public async Task Likes_AddRemoveAreIdempotentAndCountNeverNegative()
    {
        Guid postId = Guid.NewGuid();
        Guid userId = Guid.NewGuid();

        (await _likes.AddAsync(FeedPostLike.Create(postId, userId, Now).Value)).Should().BeTrue();
        (await _likes.AddAsync(FeedPostLike.Create(postId, userId, Now).Value)).Should().BeFalse();
        (await _likes.CountByPostIdAsync(postId)).Should().Be(1);

        (await _likes.RemoveAsync(postId, userId)).Should().BeTrue();
        (await _likes.RemoveAsync(postId, userId)).Should().BeFalse();
        (await _likes.CountByPostIdAsync(postId)).Should().Be(0);
    }

    [Fact]
    public async Task Likes_ParallelLikesFromSameUser_LeaveOneRecordAndCountNPlusOne()
    {
        // CA-039-04: conteo inicial N, dos dispositivos del mismo usuario marcan en paralelo.
        Guid postId = Guid.NewGuid();
        const int N = 3;
        for (int i = 0; i < N; i++)
        {
            await _likes.AddAsync(FeedPostLike.Create(postId, Guid.NewGuid(), Now).Value);
        }

        Guid userId = Guid.NewGuid();
        bool[] results = await Task.WhenAll(Enumerable.Range(0, 8)
            .Select(_ => _likes.AddAsync(FeedPostLike.Create(postId, userId, Now).Value)));

        results.Count(created => created).Should().Be(1);
        long records = await _context.FeedPostLikes.CountDocumentsAsync(l => l.PostId == postId && l.UserId == userId);
        records.Should().Be(1);
        (await _likes.CountByPostIdAsync(postId)).Should().Be(N + 1);
    }

    // ---------------- Spec 026 ----------------

    [Fact]
    public async Task Nearby_IndexIsDeclared()
    {
        List<BsonDocument> indexes = await (await _context.FeedPosts.Indexes.ListAsync()).ToListAsync();

        BsonDocument? nearby = indexes.FirstOrDefault(i => i["name"] == "ix_feed_posts_status_location_created_at");
        nearby.Should().NotBeNull();
        nearby!["key"]["location"].AsString.Should().Be("2dsphere");
    }

    [Fact]
    public async Task Nearby_ReturnsOnlyPublishedLocatedPostsInsideRadius_NewestFirst()
    {
        FeedPost older = await SeedAsync("Antigua", Now.AddHours(-2), Lat + 0.01, Lng);   // ~1.1 km
        FeedPost newer = await SeedAsync("Reciente", Now.AddHours(-1), Lat, Lng + 0.02);   // ~2.2 km
        await SeedAsync("Sin ubicación", Now, null, null);                                 // CA-026-03
        await SeedAsync("Fuera de radio", Now, Lat + 0.06, Lng);                            // ~6.7 km
        FeedPost quarantined = await SeedAsync("Cuarentena", Now, Lat, Lng);               // CA-026-02
        quarantined.MarkAsQuarantined(Now);
        await _posts.UpdateAsync(quarantined);
        FeedPost deleted = await SeedAsync("Eliminada", Now, Lat, Lng);                    // CA-026-06
        deleted.Delete(deleted.AuthorUserId, false, Now);
        await _posts.UpdateAsync(deleted);

        (IReadOnlyList<FeedPost> items, int total) = await _posts.GetPublishedNearbyPagedAsync(Lat, Lng, 5000, 1, 20);

        total.Should().Be(2);
        items.Select(p => p.Id).Should().Equal(newer.Id, older.Id);
        items.Should().OnlyContain(p => p.HasLocation);
    }

    [Fact]
    public async Task Nearby_RadiusIsInclusiveAt5KmAndRoundTripsLocation()
    {
        // ~4.9 km al norte: dentro del límite de 5 km.
        FeedPost edge = await SeedAsync("Borde", Now, Lat + 0.044, Lng);

        (IReadOnlyList<FeedPost> inside, _) = await _posts.GetPublishedNearbyPagedAsync(Lat, Lng, 5000, 1, 20);
        (IReadOnlyList<FeedPost> outside, int outsideTotal) = await _posts.GetPublishedNearbyPagedAsync(Lat, Lng, 1000, 1, 20);

        inside.Should().ContainSingle(p => p.Id == edge.Id);
        inside[0].Latitude.Should().BeApproximately(Lat + 0.044, 1e-9);
        inside[0].Longitude.Should().BeApproximately(Lng, 1e-9);
        outside.Should().BeEmpty();
        outsideTotal.Should().Be(0);
    }

    [Fact]
    public async Task Nearby_TieBreaksByIdStablyAcrossPages()
    {
        // CA-026-07: misma fecha; el _id resuelve el empate y las páginas no se solapan.
        var ids = new List<Guid>();
        for (int i = 0; i < 5; i++)
        {
            ids.Add((await SeedAsync($"Empate {i}", Now, Lat, Lng)).Id);
        }

        var seen = new List<Guid>();
        for (int page = 1; page <= 3; page++)
        {
            (IReadOnlyList<FeedPost> items, int total) = await _posts.GetPublishedNearbyPagedAsync(Lat, Lng, 5000, page, 2);
            total.Should().Be(5);
            seen.AddRange(items.Select(p => p.Id));
        }

        seen.Should().OnlyHaveUniqueItems().And.BeEquivalentTo(ids);
        seen.Select(id => id.ToString()).Should().BeInDescendingOrder(StringComparer.Ordinal);
    }

    [Fact]
    public async Task Nearby_QueryPlanUsesDeclaredIndexWithoutCollectionScan()
    {
        // El repositorio fija el índice con hint; el plan debe ser válido y no recorrer la colección.
        await SeedAsync("Con índice", Now, Lat, Lng);

        var filter = new BsonDocument
        {
            { "status", "Published" },
            { "isDeleted", false },
            {
                "location", new BsonDocument("$geoWithin", new BsonDocument(
                    "$centerSphere", new BsonArray { new BsonArray { Lng, Lat }, 5000d / 6_371_000d }))
            }
        };

        BsonDocument explain = await _context.Database.RunCommandAsync<BsonDocument>(new BsonDocument
        {
            {
                "explain", new BsonDocument
                {
                    { "find", _context.FeedPosts.CollectionNamespace.CollectionName },
                    { "filter", filter },
                    { "hint", "ix_feed_posts_status_location_created_at" }
                }
            },
            { "verbosity", "queryPlanner" }
        });

        string winningPlan = explain["queryPlanner"]["winningPlan"].ToJson();
        winningPlan.Should().Contain("ix_feed_posts_status_location_created_at");
        winningPlan.Should().NotContain("COLLSCAN");
    }

    private async Task<FeedPost> SeedAsync(string content, DateTime createdAt, double? lat, double? lng)
    {
        FeedPost post = FeedPost.Create(Guid.NewGuid(), "Autor", content, null, createdAt, latitude: lat, longitude: lng).Value;
        await _posts.AddAsync(post);
        return post;
    }

    [Fact]
    public async Task PurgeQuarantinedBeforeAsync_DeletesOnlyExpiredQuarantinedPosts()
    {
        FeedPost expiredQuarantined = FeedPost.Create(
            Guid.NewGuid(), "Autor", "Cuarentena vencida", null, Now.AddDays(-35), status: Sensus.Domain.Enums.FeedPostStatus.Quarantined).Value;
        FeedPost recentQuarantined = FeedPost.Create(
            Guid.NewGuid(), "Autor", "Cuarentena reciente", null, Now.AddDays(-5), status: Sensus.Domain.Enums.FeedPostStatus.Quarantined).Value;
        FeedPost oldPublished = FeedPost.Create(
            Guid.NewGuid(), "Autor", "Publicada antigua", null, Now.AddDays(-60), status: Sensus.Domain.Enums.FeedPostStatus.Published).Value;

        await _posts.AddAsync(expiredQuarantined);
        await _posts.AddAsync(recentQuarantined);
        await _posts.AddAsync(oldPublished);

        int purged = await _posts.PurgeQuarantinedBeforeAsync(Now.AddDays(-30));

        purged.Should().Be(1);
        (await _posts.GetByIdAsync(expiredQuarantined.Id)).Should().BeNull();
        (await _posts.GetByIdAsync(recentQuarantined.Id)).Should().NotBeNull();
        (await _posts.GetByIdAsync(oldPublished.Id)).Should().NotBeNull();
    }
}
