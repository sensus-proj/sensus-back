using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Common.Settings;
using Sensus.Application.Offers;
using Sensus.Application.Offers.DTOs;
using Sensus.Application.Offers.Queries.GetOfferFeed;
using Sensus.Domain.Entities;
using Sensus.Domain.Enums;
using Sensus.Domain.Models;
using Sensus.Domain.Repositories;
using Sensus.Domain.Services;

namespace Sensus.Application.UnitTests.Offers.Feed;

public sealed class OfferFeedRankerTests
{
    private static readonly DateTime Now = new(2026, 11, 20, 12, 0, 0, DateTimeKind.Utc);
    private static readonly FeedRankingPolicy DefaultPolicy = new FeedRankingSettings().ToDomainPolicy();

    private static FeedOfferCandidate BuildCandidate(
        Guid? offerId = null,
        string category = "Bakery",
        double distanceMeters = 500,
        DateTime? createdAt = null,
        DateTime? expiresAt = null,
        int engagement = 10) =>
        new(
            offerId ?? Guid.NewGuid(),
            $"Oferta {category}",
            category,
            "https://cdn.example.com/offer.jpg",
            100m,
            70m,
            5,
            distanceMeters,
            createdAt ?? Now.AddDays(-5),
            expiresAt ?? Now.AddHours(12),
            Guid.NewGuid(),
            "Comercio Test",
            engagement);

    [Fact]
    public void FeedRankingWeightsAndPolicy_ShouldValidateBounds()
    {
        FeedRankingWeights.Default.Validate().IsSuccess.Should().BeTrue();
        new FeedRankingWeights(-0.1, 0.2, 0.2, 0.2, 0.2).Validate().IsFailure.Should().BeTrue();
        new FeedRankingWeights(double.NaN, 0.2, 0.2, 0.2, 0.2).Validate().IsFailure.Should().BeTrue();
        new FeedRankingWeights(0, 0, 0, 0, 0).Validate().IsFailure.Should().BeTrue();

        DefaultPolicy.Validate().IsSuccess.Should().BeTrue();
        new FeedRankingPolicy(string.Empty, FeedRankingWeights.Default).Validate().IsFailure.Should().BeTrue();
        new FeedRankingPolicy("feed-v1", FeedRankingWeights.Default, MaxConsecutiveSameCategory: 0).Validate().IsFailure.Should().BeTrue();
        new FeedRankingPolicy("feed-v1", FeedRankingWeights.Default, ExplorationEvery: 101).Validate().IsFailure.Should().BeTrue();
        new FeedRankingPolicy("feed-v1", FeedRankingWeights.Default, NewOfferHours: 0).Validate().IsFailure.Should().BeTrue();
        new FeedRankingPolicy("feed-v1", FeedRankingWeights.Default, MaxSnapshotOffers: 501).Validate().IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Rank_WithExplicitInterests_ShouldPrioritizeInterestsAndRespectDiversityAndExploration()
    {
        Guid userId = Guid.NewGuid();
        var candidates = new List<FeedOfferCandidate>();

        // 15 ofertas de interés Bakery (más de 3 seguidas si no hubiera diversidad)
        for (int i = 0; i < 15; i++)
        {
            candidates.Add(BuildCandidate(
                category: "Bakery",
                distanceMeters: 200 + (i * 10),
                expiresAt: Now.AddHours(6 + i)));
        }

        // 10 ofertas de otras categorías (5 nuevas < 48h para exploración)
        for (int i = 0; i < 10; i++)
        {
            candidates.Add(BuildCandidate(
                category: i % 2 == 0 ? "Dairy" : "Pets",
                distanceMeters: 800 + (i * 20),
                createdAt: i < 5 ? Now.AddHours(-12) : Now.AddDays(-5),
                expiresAt: Now.AddHours(20 + i)));
        }

        FeedRankResult result = OfferFeedRanker.Rank(
            userId,
            candidates,
            [OfferCategory.Bakery],
            new Dictionary<string, double>(),
            radiusKm: 10,
            Now,
            DefaultPolicy);

        result.UsedFallback.Should().BeFalse();
        result.Entries.Should().HaveCount(25);

        // Top 10: al menos 60 % pertenecen al interés Bakery (CA-042-01)
        int bakeryTop10 = result.Entries.Take(10).Count(e => e.Candidate.Category == "Bakery");
        bakeryTop10.Should().BeGreaterThanOrEqualTo(6);

        // Posiciones 5, 10, 15 y 20 reservadas para exploración (CA-042-03)
        result.Entries[4].Slot.Should().Be(FeedSlot.Exploration);
        result.Entries[9].Slot.Should().Be(FeedSlot.Exploration);
        result.Entries[14].Slot.Should().Be(FeedSlot.Exploration);
        result.Entries[19].Slot.Should().Be(FeedSlot.Exploration);

        // Nunca más de 3 consecutivas de la misma categoría cuando hay alternativas (CA-042-02)
        int maxRun = GetMaxConsecutiveCategoryRun(result.Entries.Select(e => e.Candidate.Category));
        maxRun.Should().BeLessThanOrEqualTo(3);
    }

    [Fact]
    public void Rank_WhenOnlyOneCategoryExists_ShouldRelaxDiversityWithoutDroppingOffers()
    {
        List<FeedOfferCandidate> singleCategory = Enumerable.Range(1, 6)
            .Select(i => BuildCandidate(category: "Food", distanceMeters: i * 100))
            .ToList();

        FeedRankResult result = OfferFeedRanker.Rank(
            Guid.NewGuid(),
            singleCategory,
            [OfferCategory.Food],
            new Dictionary<string, double>(),
            10,
            Now,
            DefaultPolicy);

        result.RelaxedDiversity.Should().BeTrue();
        result.Entries.Should().HaveCount(6);
    }

    [Fact]
    public void Rank_WithoutInterestsOrAffinity_ShouldUseProximityAndUrgencyFallback()
    {
        FeedOfferCandidate nearUrgent = BuildCandidate(category: "Grocery", distanceMeters: 200, expiresAt: Now.AddHours(4));
        FeedOfferCandidate farLater = BuildCandidate(category: "Bakery", distanceMeters: 8000, expiresAt: Now.AddDays(6));

        FeedRankResult result = OfferFeedRanker.Rank(
            Guid.NewGuid(),
            [farLater, nearUrgent],
            Array.Empty<OfferCategory>(),
            new Dictionary<string, double>(),
            10,
            Now,
            DefaultPolicy);

        result.UsedFallback.Should().BeTrue();
        result.Entries[0].Candidate.OfferId.Should().Be(nearUrgent.OfferId);
        result.Entries[1].Candidate.OfferId.Should().Be(farLater.OfferId);
    }

    [Fact]
    public void Rank_ShouldBeDeterministicAcrossRepeatedExecutionsAndBreakTiesDeterministically()
    {
        Guid userId = Guid.NewGuid();
        Guid idLow = Guid.Parse("00000000-0000-0000-0000-000000000001");
        Guid idHigh = Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff");

        FeedOfferCandidate c1 = BuildCandidate(offerId: idHigh, category: "Bakery", distanceMeters: 500, expiresAt: Now.AddHours(10), engagement: 10);
        FeedOfferCandidate c2 = BuildCandidate(offerId: idLow, category: "Bakery", distanceMeters: 500, expiresAt: Now.AddHours(10), engagement: 10);
        FeedOfferCandidate c3 = BuildCandidate(category: "Dairy", distanceMeters: 900, createdAt: Now.AddHours(-5), expiresAt: Now.AddHours(30));

        FeedRankResult first = OfferFeedRanker.Rank(userId, [c1, c2, c3], [OfferCategory.Bakery], new Dictionary<string, double>(), 10, Now, DefaultPolicy);
        FeedRankResult second = OfferFeedRanker.Rank(userId, [c3, c2, c1], [OfferCategory.Bakery], new Dictionary<string, double>(), 10, Now, DefaultPolicy);

        first.Entries.Select(e => e.Candidate.OfferId).Should().Equal(second.Entries.Select(e => e.Candidate.OfferId));
        // Empate exacto en Bakery se resuelve por OfferId ascendente
        first.Entries[0].Candidate.OfferId.Should().Be(idLow);
    }

    [Fact]
    public void Validator_AndMapping_ShouldEnforceAllowlistAndExactDtoMapping()
    {
        var validator = new GetOfferFeedQueryValidator();
        validator.Validate(new GetOfferFeedQuery(19.4, -99.1, 10, 1, 20)).IsValid.Should().BeTrue();
        validator.Validate(new GetOfferFeedQuery(91, -99.1)).IsValid.Should().BeFalse();
        validator.Validate(new GetOfferFeedQuery(19.4, 181)).IsValid.Should().BeFalse();
        validator.Validate(new GetOfferFeedQuery(double.NaN, -99.1)).IsValid.Should().BeFalse();
        validator.Validate(new GetOfferFeedQuery(19.4, -99.1, RadiusKm: 0)).IsValid.Should().BeFalse();
        validator.Validate(new GetOfferFeedQuery(19.4, -99.1, RadiusKm: 51)).IsValid.Should().BeFalse();
        validator.Validate(new GetOfferFeedQuery(19.4, -99.1, Page: 0)).IsValid.Should().BeFalse();
        validator.Validate(new GetOfferFeedQuery(19.4, -99.1, PageSize: 101)).IsValid.Should().BeFalse();

        FeedOfferCandidate candidate = BuildCandidate(category: "Dairy", distanceMeters: 345.6);
        OfferFeedItemDto dto = candidate.ToFeedItemDto(FeedSlot.Exploration);
        dto.OfferId.Should().Be(candidate.OfferId);
        dto.Title.Should().Be(candidate.Title);
        dto.Category.Should().Be("Dairy");
        dto.DistanceMeters.Should().Be(346);
        dto.Slot.Should().Be("exploration");
        dto.FinalPrice.Should().Be(candidate.FinalPrice);
    }

    [Fact]
    public async Task Handler_WhenGuestOrMissingUser_ShouldReturn403Or401()
    {
        IOfferRepository offers = Substitute.For<IOfferRepository>();
        IDateTimeProvider clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(Now);

        ICurrentUserService guest = Substitute.For<ICurrentUserService>();
        guest.IsGuest.Returns(true);
        var guestHandler = new GetOfferFeedQueryHandler(offers, guest, clock);
        var guestRes = await guestHandler.Handle(new GetOfferFeedQuery(19.4, -99.1), CancellationToken.None);
        guestRes.IsFailure.Should().BeTrue();
        guestRes.Error.Code.Should().Be("OfferFeed.GuestForbidden");

        ICurrentUserService anon = Substitute.For<ICurrentUserService>();
        anon.IsGuest.Returns(false);
        anon.UserId.Returns(string.Empty);
        var anonHandler = new GetOfferFeedQueryHandler(offers, anon, clock);
        var anonRes = await anonHandler.Handle(new GetOfferFeedQuery(19.4, -99.1), CancellationToken.None);
        anonRes.IsFailure.Should().BeTrue();
        anonRes.Error.Code.Should().Be("OfferFeed.Unauthorized");
    }

    [Fact]
    public async Task Handler_WhenSnapshotExpiredOrRedisDegraded_ShouldRegenerateOrReturnNullSnapshotAndRecordImpressions()
    {
        Guid userId = Guid.NewGuid();
        Guid expiredSnapshotId = Guid.NewGuid();
        FeedOfferCandidate c1 = BuildCandidate(category: "Bakery", distanceMeters: 250);

        IOfferRepository offers = Substitute.For<IOfferRepository>();
        offers.GetFeedCandidatesAsync(19.4, -99.1, 10000, userId, 500, Now, Arg.Any<CancellationToken>())
            .Returns([c1]);

        ICurrentUserService user = Substitute.For<ICurrentUserService>();
        user.UserId.Returns(userId.ToString());
        user.IsGuest.Returns(false);

        IDateTimeProvider clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(Now);

        IFeedSnapshotStore snapshotStore = Substitute.For<IFeedSnapshotStore>();
        snapshotStore.GetAsync(userId, expiredSnapshotId, Arg.Any<CancellationToken>()).Returns((FeedSnapshot?)null);
        // Simula Redis no disponible al guardar
        snapshotStore.SaveAsync(userId, Arg.Any<FeedSnapshot>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(false);

        IOfferInteractionRecorder recorder = Substitute.For<IOfferInteractionRecorder>();
        IFeedMetricsPublisher metrics = Substitute.For<IFeedMetricsPublisher>();

        var handler = new GetOfferFeedQueryHandler(
            offers,
            user,
            clock,
            Options.Create(new FeedRankingSettings()),
            snapshotStore: snapshotStore,
            interactionRecorder: recorder,
            metricsPublisher: metrics);

        // Solicitar página 3 de una instantánea vencida reinicia en página 1 (CA-042-06) y degrada SnapshotId a null
        var res = await handler.Handle(new GetOfferFeedQuery(19.4, -99.1, 10, Page: 3, PageSize: 20, SnapshotId: expiredSnapshotId), CancellationToken.None);

        res.IsSuccess.Should().BeTrue();
        res.Value.Page.Should().Be(1);
        res.Value.SnapshotId.Should().BeNull();
        res.Value.RankingVersion.Should().Be("feed-v1");
        res.Value.Items.Should().ContainSingle(i => i.OfferId == c1.OfferId);

        await recorder.Received(1).RecordImpressionsAsync(
            userId,
            null,
            "feed-v1",
            1,
            Arg.Is<IReadOnlyList<OfferInteractionItem>>(items => items.Count == 1 && items[0].OfferId == c1.OfferId),
            Arg.Any<CancellationToken>());

        metrics.Received(1).PublishFeedPageServed(false, 1, Arg.Any<double>(), false, false, true, "feed-v1");
    }

    private static int GetMaxConsecutiveCategoryRun(IEnumerable<string> categories)
    {
        int maxRun = 0;
        int currentRun = 0;
        string? prev = null;

        foreach (string cat in categories)
        {
            if (string.Equals(prev, cat, StringComparison.OrdinalIgnoreCase))
            {
                currentRun++;
            }
            else
            {
                currentRun = 1;
                prev = cat;
            }

            if (currentRun > maxRun)
            {
                maxRun = currentRun;
            }
        }

        return maxRun;
    }
}
