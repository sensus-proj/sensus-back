using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Sensus.Domain.Entities;
using Sensus.Infrastructure.Persistence.Mongo;
using Sensus.Infrastructure.Persistence.Mongo.Documents;
using Sensus.Infrastructure.Persistence.Mongo.Repositories;

namespace Sensus.IntegrationTests.Persistence;

/// <summary>
/// Pruebas de persistencia y mapeo social para OfferLike y StoreReview (Specs 054, 055, FR-054-01..06, FR-055-01..03, NFR-054-02, NFR-055-02).
/// Los round-trips de documentos corren siempre; las pruebas contra MongoDB real se ejecutan cuando
/// <c>MONGO_CONNECTION_STRING</c> está definida (job <c>integration-tests</c> de CI).
/// </summary>
public sealed class MongoSocialRepositoriesTests : IAsyncLifetime
{
    private static readonly DateTime Now = new(2026, 11, 19, 18, 0, 0, DateTimeKind.Utc);

    private readonly string? _connectionString = Environment.GetEnvironmentVariable("MONGO_CONNECTION_STRING");
    private readonly string _databaseName = $"sensus_social_{Guid.NewGuid():N}";
    private MongoDbContext? _context;
    private MongoOfferLikeRepository? _offerLikes;
    private MongoStoreReviewRepository? _storeReviews;

    public Task InitializeAsync()
    {
        if (!string.IsNullOrWhiteSpace(_connectionString))
        {
            _context = new MongoDbContext(
                Options.Create(new MongoDbSettings { ConnectionString = _connectionString, DatabaseName = _databaseName }),
                NullLogger<MongoDbContext>.Instance);
            _offerLikes = new MongoOfferLikeRepository(_context);
            _storeReviews = new MongoStoreReviewRepository(_context);
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

    private bool Enabled => _context is not null;

    [Fact]
    public void OfferLikeDocument_RoundTripsDomainFields()
    {
        var userId = Guid.NewGuid();
        var offerId = Guid.NewGuid();
        var entity = OfferLike.Create(userId, offerId, Now).Value;

        var restored = OfferLikeDocument.FromDomain(entity).ToDomain();

        restored.Id.Should().Be(entity.Id);
        restored.UserId.Should().Be(userId);
        restored.OfferId.Should().Be(offerId);
        restored.CreatedAtUtc.Should().Be(Now);
    }

    [Fact]
    public void StoreReviewDocument_RoundTripsLikesAndReplies()
    {
        var merchantId = Guid.NewGuid();
        var authorId = Guid.NewGuid();
        var friendId = Guid.NewGuid();

        var review = StoreReview.Create(
            merchantId,
            authorId,
            "Ana Pérez",
            "https://cdn.example.com/ana.jpg",
            5,
            "Excelente atención y productos frescos.",
            Now).Value;
        review.ToggleLike(friendId);
        review.AddReply(friendId, "Luis Gómez", null, "Totalmente de acuerdo!", Now.AddMinutes(5));

        var restored = StoreReviewDocument.FromDomain(review).ToDomain();

        restored.Id.Should().Be(review.Id);
        restored.MerchantId.Should().Be(merchantId);
        restored.AuthorUserId.Should().Be(authorId);
        restored.AuthorName.Should().Be("Ana Pérez");
        restored.Rating.Should().Be(5);
        restored.LikedByUserIds.Should().ContainSingle().Which.Should().Be(friendId);
        restored.Replies.Should().ContainSingle(r => r.AuthorUserId == friendId && r.Comment == "Totalmente de acuerdo!");
    }

    [Fact]
    public async Task MongoOfferLikeRepository_TogglesCountsAndQueriesBatchInRealMongoDb()
    {
        if (!Enabled)
        {
            return;
        }

        var user1 = Guid.NewGuid();
        var user2 = Guid.NewGuid();
        var offer1 = Guid.NewGuid();
        var offer2 = Guid.NewGuid();

        (await _offerLikes!.ToggleLikeAsync(user1, offer1, Now)).Should().BeTrue();
        (await _offerLikes.ToggleLikeAsync(user2, offer1, Now.AddMinutes(1))).Should().BeTrue();
        (await _offerLikes.ToggleLikeAsync(user1, offer2, Now.AddMinutes(2))).Should().BeTrue();

        (await _offerLikes.CountByOfferIdAsync(offer1)).Should().Be(2);

        Dictionary<Guid, int> batchCounts = await _offerLikes.CountByOfferIdsAsync([offer1, offer2, Guid.NewGuid()]);
        batchCounts[offer1].Should().Be(2);
        batchCounts[offer2].Should().Be(1);

        HashSet<Guid> likedByUser1 = await _offerLikes.GetLikedOfferIdsAsync(user1, [offer1, offer2]);
        likedByUser1.Should().BeEquivalentTo([offer1, offer2]);

        IReadOnlyList<OfferLike> activity = await _offerLikes.GetByUserIdsAsync([user1], 10);
        activity.Should().HaveCount(2);

        (await _offerLikes.ToggleLikeAsync(user1, offer1, Now.AddMinutes(3))).Should().BeFalse();
        (await _offerLikes.CountByOfferIdAsync(offer1)).Should().Be(1);
    }

    [Fact]
    public async Task MongoStoreReviewRepository_PersistsUpdatesAndAggregatesRatingsInRealMongoDb()
    {
        if (!Enabled)
        {
            return;
        }

        var merchantId = Guid.NewGuid();
        var user1 = Guid.NewGuid();
        var user2 = Guid.NewGuid();

        var r1 = StoreReview.Create(merchantId, user1, "Ana", null, 5, "Muy buena tienda", Now).Value;
        var r2 = StoreReview.Create(merchantId, user2, "Luis", null, 4, "Buenos precios", Now.AddMinutes(2)).Value;

        await _storeReviews!.AddAsync(r1);
        await _storeReviews.AddAsync(r2);

        r1.ToggleLike(user2);
        r1.AddReply(user2, "Luis", null, "Confirmo!", Now.AddMinutes(4));
        await _storeReviews.UpdateAsync(r1);

        StoreReview? fetched = await _storeReviews.GetByIdAsync(r1.Id);
        fetched.Should().NotBeNull();
        fetched!.LikedByUserIds.Should().Contain(user2);
        fetched.Replies.Should().ContainSingle(rep => rep.Comment == "Confirmo!");

        IReadOnlyList<StoreReview> storeReviews = await _storeReviews.GetByMerchantIdAsync(merchantId);
        storeReviews.Should().HaveCount(2);

        var summaries = await _storeReviews.GetRatingSummariesByMerchantIdsAsync([merchantId]);
        summaries[merchantId].AverageRating.Should().Be(4.5);
        summaries[merchantId].ReviewCount.Should().Be(2);

        IReadOnlyList<StoreReview> recent = await _storeReviews.GetRecentActivityByUserIdsAsync([user2], 10);
        recent.Should().HaveCount(2);
    }
}
