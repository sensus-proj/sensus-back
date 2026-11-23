using FluentAssertions;
using NSubstitute;
using Sensus.Application.Comercios.Commands.CreateStoreReview;
using Sensus.Application.Comercios.Commands.ReplyStoreReview;
using Sensus.Application.Comercios.Commands.ToggleStoreReviewLike;
using Sensus.Application.Comercios.Queries.GetMyComercio;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Common.Moderation;
using Sensus.Application.Friends.Queries.GetFriendsActivity;
using Sensus.Application.Offers.Commands.ToggleOfferLike;
using Sensus.Application.Offers.Queries.GetOfferDetail;
using Sensus.Domain.Entities;
using Sensus.Domain.Enums;
using Sensus.Domain.Repositories;
using Sensus.Domain.ValueObjects;

namespace Sensus.Application.UnitTests.Social;

public sealed class SocialFeaturesAndProfanityTests
{
    private static readonly DateTime FixedNow = new(2026, 11, 19, 15, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData("pito")]
    [InlineData("puta")]
    [InlineData("PITO y PUTA en oferta")]
    [InlineData("p.i.t.o")]
    [InlineData("puuuuuta")]
    [InlineData("p1t0")]
    [InlineData("pUt4")]
    [InlineData("Tienda El Pendejo")]
    public void ProfanityFilter_ShouldDetectOffensiveWords(string input)
    {
        ProfanityFilter.ContainsProfanity(input).Should().BeTrue();
    }

    [Theory]
    [InlineData("Computadora Gamer HP")]
    [InlineData("Disputa de precios justa")]
    [InlineData("Pistón hidráulico nuevo")]
    [InlineData("Peras dulces de temporada")]
    [InlineData("Panadería La Esperanza")]
    public void ProfanityFilter_ShouldAllowCleanSpanishText(string input)
    {
        ProfanityFilter.ContainsProfanity(input).Should().BeFalse();
    }

    [Fact]
    public async Task ContentModerationHelper_ShouldRejectProfanity_WithoutCallingOpenAi()
    {
        var moderation = Substitute.For<ITextModerationService>();

        var result = await ContentModerationHelper.EnsureApprovedAsync(
            moderation,
            "Oferta de pito y puta",
            "Publicación",
            "Offer.ContentModerationRejected",
            "Rechazado",
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Offer.ContentModerationRejected");
        await moderation.DidNotReceiveWithAnyArgs()
            .ModerateContentTextAsync(default!, default!, default);
    }

    [Fact]
    public async Task ToggleOfferLike_ShouldIncrementAndDecrementCount_AndReflectInOfferDetail()
    {
        var ownerId = Guid.NewGuid();
        var buyerId = Guid.NewGuid();
        var comercio = CreateComercio(ownerId, "Café Sensus");
        var offer = Offer.Create(
            comercio.Id,
            "Latte 2x1",
            "Café de especialidad",
            100m,
            50,
            5,
            FixedNow.AddHours(-1),
            FixedNow.AddDays(2),
            imageUrl: null,
            latitude: comercio.Latitud,
            longitude: comercio.Longitud,
            nowUtc: FixedNow).Value;

        var offerRepo = Substitute.For<IOfferRepository>();
        offerRepo.GetByIdAsync(offer.Id, Arg.Any<CancellationToken>()).Returns(offer);

        var comercioRepo = Substitute.For<IComercioRepository>();
        comercioRepo.GetByIdAsync(comercio.Id, Arg.Any<CancellationToken>()).Returns(comercio);

        var claimRepo = Substitute.For<IOfferClaimRepository>();
        var likeRepo = new StubOfferLikeRepository();
        var currentUser = MockCurrentUser(buyerId);
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(FixedNow);

        var toggleHandler = new ToggleOfferLikeCommandHandler(offerRepo, likeRepo, currentUser, clock);
        var likeOn = await toggleHandler.Handle(new ToggleOfferLikeCommand(offer.Id), CancellationToken.None);

        likeOn.IsSuccess.Should().BeTrue();
        likeOn.Value.LikesCount.Should().Be(1);
        likeOn.Value.IsLikedByMe.Should().BeTrue();

        var detailHandler = new GetOfferDetailQueryHandler(
            offerRepo,
            comercioRepo,
            claimRepo,
            currentUser,
            offerLikeRepository: likeRepo);

        var detailWithLike = await detailHandler.Handle(new GetOfferDetailQuery(offer.Id), CancellationToken.None);
        detailWithLike.IsSuccess.Should().BeTrue();
        detailWithLike.Value.LikesCount.Should().Be(1);
        detailWithLike.Value.IsLikedByMe.Should().BeTrue();

        var likeOff = await toggleHandler.Handle(new ToggleOfferLikeCommand(offer.Id), CancellationToken.None);
        likeOff.IsSuccess.Should().BeTrue();
        likeOff.Value.LikesCount.Should().Be(0);
        likeOff.Value.IsLikedByMe.Should().BeFalse();
    }

    [Fact]
    public async Task StoreReviews_ShouldComputeRealAverage_SupportLikesAndReplies_AndUpdateMerchantProfile()
    {
        var merchantOwnerId = Guid.NewGuid();
        var buyerA = CreateUser("Ana López");
        var buyerB = CreateUser("Luis Pérez");
        var comercio = CreateComercio(merchantOwnerId, "Tacos Don Pepe");

        var comercioRepo = Substitute.For<IComercioRepository>();
        comercioRepo.GetByIdAsync(comercio.Id, Arg.Any<CancellationToken>()).Returns(comercio);
        comercioRepo.GetByUsuarioIdAsync(merchantOwnerId, Arg.Any<CancellationToken>()).Returns(comercio);

        var userRepo = Substitute.For<IUserRepository>();
        userRepo.GetByIdAsync(buyerA.Id, Arg.Any<CancellationToken>()).Returns(buyerA);
        userRepo.GetByIdAsync(buyerB.Id, Arg.Any<CancellationToken>()).Returns(buyerB);

        var reviewRepo = new StubStoreReviewRepository();
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(FixedNow);
        var moderation = ApprovingModeration();
        var auditRecorder = Substitute.For<IModerationAuditRecorder>();
        auditRecorder.RecordAsync(Arg.Any<ModerationAuditEntry>(), Arg.Any<CancellationToken>())
            .Returns(Sensus.Domain.Common.Result.Success());

        // Calificación 5 y calificación 4 => promedio 4.5
        var createAsBuyerA = new CreateStoreReviewCommandHandler(
            comercioRepo,
            userRepo,
            reviewRepo,
            MockCurrentUser(buyerA.Id),
            clock,
            auditRecorder,
            moderation);

        var firstSummary = await createAsBuyerA.Handle(
            new CreateStoreReviewCommand(comercio.Id, 5, "Excelente atención y tacos frescos"),
            CancellationToken.None);
        firstSummary.IsSuccess.Should().BeTrue();

        // Bloquea groserías en reseñas
        var offensiveReview = await createAsBuyerA.Handle(
            new CreateStoreReviewCommand(comercio.Id, 1, "Son una puta basura"),
            CancellationToken.None);
        offensiveReview.IsFailure.Should().BeTrue();
        offensiveReview.Error.Code.Should().Be("StoreReview.ContentModerationRejected");

        clock.UtcNow.Returns(FixedNow.AddMinutes(5));
        var createAsBuyerB = new CreateStoreReviewCommandHandler(
            comercioRepo,
            userRepo,
            reviewRepo,
            MockCurrentUser(buyerB.Id),
            clock,
            auditRecorder,
            moderation);

        var secondSummary = await createAsBuyerB.Handle(
            new CreateStoreReviewCommand(comercio.Id, 4, "Muy buen sabor y precios"),
            CancellationToken.None);

        secondSummary.IsSuccess.Should().BeTrue();
        secondSummary.Value.AverageRating.Should().Be(4.5);
        secondSummary.Value.TotalReviews.Should().Be(2);

        Guid reviewId = secondSummary.Value.Reviews[0].Id;

        var likeReviewHandler = new ToggleStoreReviewLikeCommandHandler(reviewRepo, MockCurrentUser(buyerA.Id));
        var likedReview = await likeReviewHandler.Handle(new ToggleStoreReviewLikeCommand(reviewId), CancellationToken.None);
        likedReview.IsSuccess.Should().BeTrue();
        likedReview.Value.LikesCount.Should().Be(1);
        likedReview.Value.IsLikedByMe.Should().BeTrue();

        var replyHandler = new ReplyStoreReviewCommandHandler(
            reviewRepo,
            userRepo,
            MockCurrentUser(buyerA.Id),
            clock,
            auditRecorder,
            moderation);

        var badReply = await replyHandler.Handle(
            new ReplyStoreReviewCommand(reviewId, "No digas pito"),
            CancellationToken.None);
        badReply.IsFailure.Should().BeTrue();

        var goodReply = await replyHandler.Handle(
            new ReplyStoreReviewCommand(reviewId, "Totalmente de acuerdo, volveré pronto"),
            CancellationToken.None);
        goodReply.IsSuccess.Should().BeTrue();
        goodReply.Value.Replies.Should().HaveCount(1);
        goodReply.Value.Replies[0].AuthorName.Should().Be("Ana López");

        var myComercioHandler = new GetMyComercioQueryHandler(
            comercioRepo,
            MockCurrentUser(merchantOwnerId),
            storeReviewRepository: reviewRepo);

        var myComercio = await myComercioHandler.Handle(new GetMyComercioQuery(), CancellationToken.None);
        myComercio.IsSuccess.Should().BeTrue();
        myComercio.Value.AverageRating.Should().Be(4.5);
        myComercio.Value.ReviewCount.Should().Be(2);
    }

    [Fact]
    public async Task GetFriendsCommunityActivity_ShouldReturnOnlyFriendsAndSelfCommentsAndLikes()
    {
        var me = CreateUser("Beto");
        var friend = CreateUser("Carlos");
        var stranger = CreateUser("Desconocido");
        var merchant = CreateComercio(Guid.NewGuid(), "Café Central");

        var offer = Offer.Create(
            merchant.Id,
            "Capuchino Vainilla",
            "Taza grande",
            80m,
            25,
            10,
            FixedNow.AddHours(-2),
            FixedNow.AddDays(3),
            imageUrl: null,
            latitude: merchant.Latitud,
            longitude: merchant.Longitud,
            nowUtc: FixedNow).Value;

        var friendRequest = FriendRequest.Create(
            me.Id,
            "Beto",
            "CODE-BETO",
            friend.Id,
            "Carlos",
            "CODE-CARLOS",
            "invite_code",
            FixedNow.AddDays(-2)).Value;
        friendRequest.Accept(FixedNow.AddDays(-1));

        var friendRepo = Substitute.For<IFriendRequestRepository>();
        friendRepo.GetForUserAsync(me.Id, Arg.Any<CancellationToken>()).Returns([friendRequest]);

        var offerRepo = Substitute.For<IOfferRepository>();
        offerRepo.GetByIdAsync(offer.Id, Arg.Any<CancellationToken>()).Returns(offer);

        var comercioRepo = Substitute.For<IComercioRepository>();
        comercioRepo.GetByIdAsync(merchant.Id, Arg.Any<CancellationToken>()).Returns(merchant);

        var userRepo = Substitute.For<IUserRepository>();
        userRepo.GetByIdAsync(me.Id, Arg.Any<CancellationToken>()).Returns(me);
        userRepo.GetByIdAsync(friend.Id, Arg.Any<CancellationToken>()).Returns(friend);
        userRepo.GetByIdAsync(stranger.Id, Arg.Any<CancellationToken>()).Returns(stranger);

        var likeRepo = new StubOfferLikeRepository();
        await likeRepo.ToggleLikeAsync(friend.Id, offer.Id, FixedNow.AddMinutes(-10));
        await likeRepo.ToggleLikeAsync(stranger.Id, offer.Id, FixedNow.AddMinutes(-5));

        var reviewRepo = new StubStoreReviewRepository();
        var myReview = StoreReview.Create(merchant.Id, me.Id, me.NombreCompleto, null, 5, "Me encantó el café", FixedNow.AddMinutes(-3)).Value;
        var strangerReview = StoreReview.Create(merchant.Id, stranger.Id, stranger.NombreCompleto, null, 2, "Comentario ajeno", FixedNow.AddMinutes(-1)).Value;
        await reviewRepo.AddAsync(myReview);
        await reviewRepo.AddAsync(strangerReview);

        var handler = new GetFriendsCommunityActivityQueryHandler(
            MockCurrentUser(me.Id),
            friendRepo,
            likeRepo,
            reviewRepo,
            offerRepo,
            comercioRepo,
            userRepo);

        var result = await handler.Handle(new GetFriendsCommunityActivityQuery(20), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value.Select(i => i.ActorUserId).Should().NotContain(stranger.Id);
        result.Value[0].ActivityType.Should().Be(GetFriendsCommunityActivityQueryHandler.ActivityStoreReview);
        result.Value[1].ActivityType.Should().Be(GetFriendsCommunityActivityQueryHandler.ActivityOfferLike);
    }

    private static ICurrentUserService MockCurrentUser(Guid userId)
    {
        var current = Substitute.For<ICurrentUserService>();
        current.IsAuthenticated.Returns(true);
        current.IsGuest.Returns(false);
        current.UserId.Returns(userId.ToString());
        return current;
    }

    private static ITextModerationService ApprovingModeration()
    {
        var mod = Substitute.For<ITextModerationService>();
        mod.ModerateContentTextAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new TextModerationResult(true, "Aprobado", [], "Approved"));
        return mod;
    }

    private static User CreateUser(string name) =>
        User.Create(name, $"{Guid.NewGuid():N}@sensus.com", "hashed-pw", true, FixedNow).Value;

    private static Comercio CreateComercio(Guid ownerId, string name) =>
        Comercio.Create(
            ownerId,
            name,
            "Av. Reforma 123",
            "06000",
            19.4326,
            -99.1332,
            FixedNow,
            MerchantCategory.Cafeterias).Value;

    private sealed class StubOfferLikeRepository : IOfferLikeRepository
    {
        private readonly List<OfferLike> _likes = [];

        public Task<bool> ToggleLikeAsync(Guid userId, Guid offerId, DateTime nowUtc, CancellationToken ct = default)
        {
            var existing = _likes.FirstOrDefault(l => l.UserId == userId && l.OfferId == offerId);
            if (existing is not null)
            {
                _likes.Remove(existing);
                return Task.FromResult(false);
            }

            _likes.Add(OfferLike.Create(userId, offerId, nowUtc).Value);
            return Task.FromResult(true);
        }

        public Task<int> CountByOfferIdAsync(Guid offerId, CancellationToken ct = default) =>
            Task.FromResult(_likes.Count(l => l.OfferId == offerId));

        public Task<Dictionary<Guid, int>> CountByOfferIdsAsync(IEnumerable<Guid> offerIds, CancellationToken ct = default) =>
            Task.FromResult(_likes.Where(l => offerIds.Contains(l.OfferId)).GroupBy(l => l.OfferId).ToDictionary(g => g.Key, g => g.Count()));

        public Task<HashSet<Guid>> GetLikedOfferIdsAsync(Guid userId, IEnumerable<Guid> offerIds, CancellationToken ct = default) =>
            Task.FromResult(_likes.Where(l => l.UserId == userId && offerIds.Contains(l.OfferId)).Select(l => l.OfferId).ToHashSet());

        public Task<IReadOnlyList<OfferLike>> GetByUserIdsAsync(IEnumerable<Guid> userIds, int limit, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<OfferLike>>(_likes.Where(l => userIds.Contains(l.UserId)).OrderByDescending(l => l.CreatedAtUtc).Take(limit).ToList());
    }

    private sealed class StubStoreReviewRepository : IStoreReviewRepository
    {
        private readonly List<StoreReview> _reviews = [];

        public Task<StoreReview?> GetByIdAsync(Guid reviewId, CancellationToken ct = default) =>
            Task.FromResult(_reviews.FirstOrDefault(r => r.Id == reviewId));

        public Task<IReadOnlyList<StoreReview>> GetByMerchantIdAsync(Guid merchantId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<StoreReview>>(_reviews.Where(r => r.MerchantId == merchantId).OrderByDescending(r => r.CreatedAtUtc).ToList());

        public Task<(IReadOnlyList<StoreReview> Items, long TotalCount)> GetPagedByMerchantIdAsync(
            Guid merchantId,
            int pageNumber,
            int pageSize,
            CancellationToken ct = default)
        {
            var matching = _reviews.Where(r => r.MerchantId == merchantId).OrderByDescending(r => r.CreatedAtUtc).ToList();
            int skip = (Math.Max(1, pageNumber) - 1) * Math.Max(1, pageSize);
            IReadOnlyList<StoreReview> paged = matching.Skip(skip).Take(Math.Max(1, pageSize)).ToList();
            return Task.FromResult((paged, (long)matching.Count));
        }

        public Task<StoreReviewRatingStats> GetRatingSummaryAsync(
            Guid merchantId,
            CancellationToken ct = default)
        {
            var matching = _reviews.Where(r => r.MerchantId == merchantId).ToList();
            int total = matching.Count;
            double avg = total == 0 ? 0.0 : Math.Round(matching.Average(r => r.Rating), 1);
            var counts = matching.GroupBy(r => r.Rating).ToDictionary(g => g.Key, g => g.Count());
            return Task.FromResult(new StoreReviewRatingStats(avg, total, counts));
        }

        public Task<Dictionary<Guid, (double AverageRating, int ReviewCount)>> GetRatingSummariesByMerchantIdsAsync(
            IEnumerable<Guid> merchantIds,
            CancellationToken ct = default) =>
            Task.FromResult(_reviews
                .Where(r => merchantIds.Contains(r.MerchantId))
                .GroupBy(r => r.MerchantId)
                .ToDictionary(g => g.Key, g => (Math.Round(g.Average(x => x.Rating), 1), g.Count())));

        public Task<IReadOnlyList<StoreReview>> GetRecentActivityByUserIdsAsync(
            IEnumerable<Guid> userIds,
            int limit,
            CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<StoreReview>>(_reviews
                .Where(r => userIds.Contains(r.AuthorUserId) || r.Replies.Any(x => userIds.Contains(x.AuthorUserId)))
                .OrderByDescending(r => r.CreatedAtUtc)
                .Take(limit)
                .ToList());

        public Task AddAsync(StoreReview review, CancellationToken ct = default)
        {
            _reviews.Add(review);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(StoreReview review, CancellationToken ct = default)
        {
            int idx = _reviews.FindIndex(r => r.Id == review.Id);
            if (idx >= 0)
            {
                _reviews[idx] = review;
            }

            return Task.CompletedTask;
        }
    }
}
