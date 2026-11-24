using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Sensus.Domain.Entities;
using Sensus.Domain.Enums;
using Sensus.Infrastructure.Persistence.Mongo;
using Sensus.Infrastructure.Persistence.Mongo.Documents;
using Sensus.Infrastructure.Persistence.Mongo.Repositories;

namespace Sensus.IntegrationTests.Persistence;

/// <summary>
/// Pruebas de persistencia y mapeo social para OfferLike y StoreReview (Specs 056, 057, FR-056-01..06, FR-057-01..03, NFR-056-02, NFR-057-02).
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
    private MongoFriendRequestRepository? _friendRequests;
    private MongoSharedOfferRepository? _sharedOffers;
    private MongoFeedCommentRepository? _feedComments;
    private MongoContentReportRepository? _contentReports;
    private MongoOfferStatusHistoryRepository? _offerHistory;

    public Task InitializeAsync()
    {
        if (!string.IsNullOrWhiteSpace(_connectionString))
        {
            _context = new MongoDbContext(
                Options.Create(new MongoDbSettings { ConnectionString = _connectionString, DatabaseName = _databaseName }),
                NullLogger<MongoDbContext>.Instance);
            _offerLikes = new MongoOfferLikeRepository(_context);
            _storeReviews = new MongoStoreReviewRepository(_context);
            _friendRequests = new MongoFriendRequestRepository(_context);
            _sharedOffers = new MongoSharedOfferRepository(_context);
            _feedComments = new MongoFeedCommentRepository(_context);
            _contentReports = new MongoContentReportRepository(_context);
            _offerHistory = new MongoOfferStatusHistoryRepository(_context);
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

    [Fact]
    public void NewDocuments_RoundTripDomainFields()
    {
        var comment = FeedComment.Create(Guid.NewGuid(), Guid.NewGuid(), "Sofía", "Excelente oferta", Now).Value;
        var restoredComment = FeedCommentDocument.FromDomain(comment).ToDomain();
        restoredComment.Id.Should().Be(comment.Id);
        restoredComment.Text.Should().Be("Excelente oferta");

        var transition = new OfferStatusTransition(Guid.NewGuid(), OfferStatus.Published, OfferStatus.Paused, Guid.NewGuid(), "Manual", Now);
        var restoredTransition = OfferStatusHistoryDocument.FromDomain(transition).ToDomain();
        restoredTransition.OfferId.Should().Be(transition.OfferId);
        restoredTransition.ToStatus.Should().Be(OfferStatus.Paused);

        var friendReq = FriendRequest.Create(Guid.NewGuid(), "Ana", "SNS-1", Guid.NewGuid(), "Luis", "SNS-2", "QR", Now).Value;
        var restoredFriend = FriendRequestDocument.FromDomain(friendReq).ToDomain();
        restoredFriend.SenderInviteCode.Should().Be("SNS-1");

        var shared = SharedOffer.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Café", "Tienda", null, null, 50m, 30m, 40, null, Now).Value;
        var restoredShared = SharedOfferDocument.FromDomain(shared).ToDomain();
        restoredShared.OfferTitle.Should().Be("Café");

        var report = ContentReport.Create(Guid.NewGuid(), ReportTargetType.Offer, Guid.NewGuid(), ReportReason.Fraud, "Precio distinto", Now).Value;
        var reportDoc = ContentReportDocument.FromDomain(report);
        reportDoc.TargetId.Should().Be(report.TargetId);
        reportDoc.Reason.Should().Be("Fraud");
    }

    [Fact]
    public async Task MongoFriendFeedCommentReportAndHistoryRepositories_PersistInRealMongoDb()
    {
        if (!Enabled)
        {
            return;
        }

        Guid postId = Guid.NewGuid();
        Guid user1 = Guid.NewGuid();
        Guid user2 = Guid.NewGuid();

        var comment = FeedComment.Create(postId, user1, "Sofía", "Primer comentario", Now).Value;
        await _feedComments!.AddAsync(comment);
        (await _feedComments.CountByPostIdAsync(postId)).Should().Be(1);
        (await _feedComments.GetByPostIdPagedAsync(postId, 1, 20)).Should().ContainSingle(c => c.Id == comment.Id);

        var report = ContentReport.Create(user1, ReportTargetType.Comment, comment.Id, ReportReason.Spam, "Prueba", Now).Value;
        (await _contentReports!.TryAddAsync(report)).Should().BeTrue();
        (await _contentReports.TryAddAsync(report)).Should().BeFalse();
        (await _contentReports.CountRecentByTargetAsync(ReportTargetType.Comment, comment.Id, Now.AddHours(-1))).Should().Be(1);

        var transition = new OfferStatusTransition(Guid.NewGuid(), OfferStatus.Published, OfferStatus.Expired, null, "AutomaticExpiration:1", Now);
        await _offerHistory!.AddAsync(transition);
        (await _offerHistory.GetByOfferIdAsync(transition.OfferId)).Should().ContainSingle();

        var fr = FriendRequest.Create(user1, "Ana", "SNS-A1", user2, "Luis", "SNS-B2", "QR", Now).Value;
        await _friendRequests!.AddAsync(fr);
        (await _friendRequests.GetForUserAsync(user1)).Should().ContainSingle(r => r.Id == fr.Id);

        var shared = SharedOffer.Create(user1, user2, Guid.NewGuid(), "2x1 en café", "Café", null, null, 60m, 30m, 50, null, Now).Value;
        await _sharedOffers!.AddRangeAsync([shared]);
        (await _sharedOffers!.GetConversationAsync(user1, user2)).Should().ContainSingle(s => s.Id == shared.Id);
    }
}
