using FluentAssertions;
using NSubstitute;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Common.Settings;
using Sensus.Application.Offers.Commands.ClaimOffer;
using Sensus.Application.Offers.Commands.RegisterOfferView;
using Sensus.Application.Offers.DTOs;
using Sensus.Domain.Common;
using Sensus.Domain.Entities;
using Sensus.Domain.Enums;
using Sensus.Domain.Repositories;

namespace Sensus.Application.UnitTests.Offers.Interactions;

public sealed class OfferInteractionTests
{
    private static readonly DateTime Now = new(2026, 11, 20, 12, 0, 0, DateTimeKind.Utc);
    private static readonly string ValidHex = new('a', 64);

    [Theory]
    [InlineData(null, DistanceBucket.Unknown, "unknown")]
    [InlineData(-0.5, DistanceBucket.Unknown, "unknown")]
    [InlineData(double.NaN, DistanceBucket.Unknown, "unknown")]
    [InlineData(0.0, DistanceBucket.Range0To1Km, "0-1")]
    [InlineData(0.99, DistanceBucket.Range0To1Km, "0-1")]
    [InlineData(1.0, DistanceBucket.Range1To3Km, "1-3")]
    [InlineData(2.99, DistanceBucket.Range1To3Km, "1-3")]
    [InlineData(3.0, DistanceBucket.Range3To10Km, "3-10")]
    [InlineData(10.0, DistanceBucket.Range3To10Km, "3-10")]
    [InlineData(10.01, DistanceBucket.Range10PlusKm, "10+")]
    public void DistanceBucketClassifier_ShouldClassifyAndSerializeDeterministically(
        double? distanceKm,
        DistanceBucket expectedBucket,
        string expectedWire)
    {
        DistanceBucket bucket = OfferInteractionCatalog.ClassifyDistance(distanceKm);
        bucket.Should().Be(expectedBucket);
        bucket.ToWireName().Should().Be(expectedWire);
        OfferInteractionCatalog.ParseDistanceBucket(expectedWire).Should().Be(expectedBucket);
    }

    [Fact]
    public void OfferInteractionItem_ShouldNormalizeLegacyCategoriesAndDefaultNull()
    {
        OfferInteractionItem normalized = OfferInteractionItem.Create(
            Guid.NewGuid(), 1, FeedSlot.Ranked, "Panadería", DistanceBucket.Range0To1Km).Value;
        normalized.Category.Should().Be("Bakery");

        OfferInteractionItem uncategorized = OfferInteractionItem.Create(
            Guid.NewGuid(), 2, FeedSlot.Exploration, null, DistanceBucket.Unknown).Value;
        uncategorized.Category.Should().Be(OfferInteractionItem.UncategorizedCategory);

        OfferInteractionItem.Create(Guid.Empty, 1, FeedSlot.Ranked, "Bakery", DistanceBucket.Range0To1Km)
            .IsFailure.Should().BeTrue();
        OfferInteractionItem.Create(Guid.NewGuid(), 0, FeedSlot.Ranked, "Bakery", DistanceBucket.Range0To1Km)
            .IsFailure.Should().BeTrue();
    }

    [Fact]
    public void OfferInteraction_ShouldEnforcePiiSafeAndSizeInvariants()
    {
        OfferInteractionItem item = OfferInteractionItem.Create(
            Guid.NewGuid(), 1, FeedSlot.Ranked, "Bakery", DistanceBucket.Range0To1Km).Value;

        // Nunca acepta un Guid o texto en claro como pseudoUserId (CA-055-02 / NFR-055-02)
        OfferInteraction.Create(OfferInteractionType.Open, Guid.NewGuid().ToString(), 1, null, "direct", 1, [item], Now)
            .IsFailure.Should().BeTrue();
        OfferInteraction.Create(OfferInteractionType.Open, "user@example.com", 1, null, "direct", 1, [item], Now)
            .IsFailure.Should().BeTrue();
        OfferInteraction.Create(OfferInteractionType.Open, ValidHex, 0, null, "direct", 1, [item], Now)
            .IsFailure.Should().BeTrue();
        OfferInteraction.Create(OfferInteractionType.Open, ValidHex, 1, null, string.Empty, 1, [item], Now)
            .IsFailure.Should().BeTrue();
        OfferInteraction.Create(OfferInteractionType.Open, ValidHex, 1, null, "direct", 0, [item], Now)
            .IsFailure.Should().BeTrue();
        OfferInteraction.Create(OfferInteractionType.Open, ValidHex, 1, null, "direct", 1, [], Now)
            .IsFailure.Should().BeTrue();

        List<OfferInteractionItem> twentyItems = Enumerable.Range(1, 20)
            .Select(i => OfferInteractionItem.Create(Guid.NewGuid(), i, FeedSlot.Ranked, "Dairy", DistanceBucket.Range1To3Km).Value)
            .ToList();

        // open y claim exigen exactamente 1 elemento
        OfferInteraction.Create(OfferInteractionType.Open, ValidHex, 1, null, "direct", 1, twentyItems, Now)
            .IsFailure.Should().BeTrue();

        // impression acepta página completa hasta 100 elementos (CA-055-01)
        Result<OfferInteraction> pageResult = OfferInteraction.Create(
            OfferInteractionType.Impression, ValidHex, 2, "snap-1", "feed-v1", 1, twentyItems, Now);
        pageResult.IsSuccess.Should().BeTrue();
        pageResult.Value.Items.Should().HaveCount(20);
        pageResult.Value.KeyVersion.Should().Be(2);

        // Más de 100 impresiones por página rechazadas
        List<OfferInteractionItem> tooMany = Enumerable.Range(1, 101)
            .Select(i => OfferInteractionItem.Create(Guid.NewGuid(), i, FeedSlot.Ranked, "Dairy", DistanceBucket.Range1To3Km).Value)
            .ToList();
        OfferInteraction.Create(OfferInteractionType.Impression, ValidHex, 1, "snap-1", "feed-v1", 1, tooMany, Now)
            .IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task RegisterOfferView_AsGuest_ShouldNotRecordOpenInteraction()
    {
        Guid guestId = Guid.NewGuid();
        Offer offer = Offer.Create(Guid.NewGuid(), "Pan fresco", "Bolillo artesanal", 20m, 10, 5, Now.AddHours(-1), Now.AddDays(1), null, 19.4, -99.1, Now, "Bakery").Value;

        IOfferRepository offers = Substitute.For<IOfferRepository>();
        offers.GetByIdAsync(offer.Id, Arg.Any<CancellationToken>()).Returns(offer);
        offers.IncrementViewCountAsync(offer.Id, Arg.Any<CancellationToken>()).Returns(true);

        IOfferViewRepository views = Substitute.For<IOfferViewRepository>();
        views.TryRegisterAsync(Arg.Any<OfferView>(), Arg.Any<CancellationToken>()).Returns(true);

        ICurrentUserService currentUser = Substitute.For<ICurrentUserService>();
        currentUser.UserId.Returns(guestId.ToString());
        currentUser.IsGuest.Returns(true);

        IDateTimeProvider clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(Now);

        IOfferInteractionRecorder recorder = Substitute.For<IOfferInteractionRecorder>();
        var handler = new RegisterOfferViewCommandHandler(offers, views, currentUser, clock, recorder);

        Result result = await handler.Handle(new RegisterOfferViewCommand(offer.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await recorder.DidNotReceiveWithAnyArgs().RecordSingleAsync(default, default, null!, null!, null, default);
    }

    [Fact]
    public async Task RegisterOfferView_AsRegisteredUser_ShouldRecordOpenInteraction()
    {
        Guid userId = Guid.NewGuid();
        Offer offer = Offer.Create(Guid.NewGuid(), "Leche 1L", "Deslactosada", 30m, 20, 10, Now.AddHours(-1), Now.AddDays(1), null, 19.4, -99.1, Now, "Dairy").Value;

        IOfferRepository offers = Substitute.For<IOfferRepository>();
        offers.GetByIdAsync(offer.Id, Arg.Any<CancellationToken>()).Returns(offer);
        offers.IncrementViewCountAsync(offer.Id, Arg.Any<CancellationToken>()).Returns(true);

        IOfferViewRepository views = Substitute.For<IOfferViewRepository>();
        views.TryRegisterAsync(Arg.Any<OfferView>(), Arg.Any<CancellationToken>()).Returns(true);

        ICurrentUserService currentUser = Substitute.For<ICurrentUserService>();
        currentUser.UserId.Returns(userId.ToString());
        currentUser.IsGuest.Returns(false);

        IDateTimeProvider clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(Now);

        IOfferInteractionRecorder recorder = Substitute.For<IOfferInteractionRecorder>();
        var handler = new RegisterOfferViewCommandHandler(offers, views, currentUser, clock, recorder);

        Result result = await handler.Handle(new RegisterOfferViewCommand(offer.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await recorder.Received(1).RecordSingleAsync(
            userId,
            OfferInteractionType.Open,
            Arg.Is<OfferInteractionItem>(i => i.OfferId == offer.Id && i.Category == "Dairy" && i.Slot == FeedSlot.Direct),
            "direct",
            null,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ClaimOffer_AsRegisteredUser_ShouldRecordClaimInteraction()
    {
        Guid userId = Guid.NewGuid();
        Offer offer = Offer.Create(Guid.NewGuid(), "Croissant", "Mantequilla", 40m, 25, 5, Now.AddHours(-1), Now.AddDays(1), null, 19.4, -99.1, Now, "Bakery").Value;

        IOfferRepository offers = Substitute.For<IOfferRepository>();
        offers.GetByIdAsync(offer.Id, Arg.Any<CancellationToken>()).Returns(offer);
        offers.TryReserveStockAsync(offer.Id, Now, Arg.Any<CancellationToken>()).Returns(true);

        IOfferClaimRepository claims = Substitute.For<IOfferClaimRepository>();
        claims.GetActiveByUserAndOfferAsync(userId, offer.Id, Arg.Any<CancellationToken>()).Returns((OfferClaim?)null);
        claims.TryAddAsync(Arg.Any<OfferClaim>(), Arg.Any<CancellationToken>()).Returns(true);

        ICurrentUserService currentUser = Substitute.For<ICurrentUserService>();
        currentUser.UserId.Returns(userId.ToString());
        currentUser.IsGuest.Returns(false);

        IClaimCodeService codeService = Substitute.For<IClaimCodeService>();
        codeService.Generate(Arg.Any<Guid>(), offer.Id, Arg.Any<DateTime>())
            .Returns(new ClaimCode("ABC-123", new('b', 64), "{\"code\":\"ABC-123\"}"));

        IDateTimeProvider clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(Now);

        ICacheService cache = Substitute.For<ICacheService>();
        IOfferInteractionRecorder recorder = Substitute.For<IOfferInteractionRecorder>();
        var handler = new ClaimOfferCommandHandler(offers, claims, currentUser, codeService, clock, cache, null, recorder);

        Result<ClaimOfferResultDto> result = await handler.Handle(new ClaimOfferCommand(offer.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await recorder.Received(1).RecordSingleAsync(
            userId,
            OfferInteractionType.Claim,
            Arg.Is<OfferInteractionItem>(i => i.OfferId == offer.Id && i.Category == "Bakery" && i.Slot == FeedSlot.Direct),
            "direct",
            null,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public void InteractionSettings_ShouldProvideSafeFreeTierDefaults()
    {
        var settings = new InteractionSettings();
        settings.Enabled.Should().BeTrue();
        settings.ImpressionSampleRate.Should().Be(1.0);
        settings.RetentionDays.Should().Be(180);
        settings.WriteTimeoutMs.Should().Be(200);
        settings.CollectionLimitBytes.Should().Be(150L * 1024 * 1024);
        settings.ClusterLimitBytes.Should().Be(512L * 1024 * 1024);
        settings.AlarmThresholdPercent.Should().Be(70);
        settings.EmitDetailedCustomMetrics.Should().BeFalse();
    }
}
