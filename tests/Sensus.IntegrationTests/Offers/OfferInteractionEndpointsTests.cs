using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Common.Settings;
using Sensus.Domain.Entities;
using Sensus.Domain.Enums;
using Sensus.Domain.Repositories;
using Sensus.Infrastructure.Observability;
using Sensus.Infrastructure.Security;
using Sensus.Infrastructure.Services;
using Sensus.IntegrationTests.Infrastructure;

namespace Sensus.IntegrationTests.Offers;

/// <summary>
/// Pruebas de integración para Spec 055: registro HMAC sin PII, resiliencia ante fallo de escritura,
/// métricas EMF en Free Tier, purga multi-versión y ausencia de eventos para invitados.
/// </summary>
public sealed class OfferInteractionEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private static readonly DateTime Now = new(2026, 11, 20, 12, 0, 0, DateTimeKind.Utc);
    private readonly CustomWebApplicationFactory _factory;

    public OfferInteractionEndpointsTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _factory.FakeClock.Set(DateTime.UtcNow);
        _factory.OfferRepository.ClearReceivedCalls();
        _factory.OfferViewRepository.ClearReceivedCalls();
        _factory.OfferClaimRepository.ClearReceivedCalls();
        _factory.OfferInteractionRecorder.ClearReceivedCalls();
    }

    private (string Token, User User) CreateRegisteredToken()
    {
        User user = User.Create("Consumidor", $"user.{Guid.NewGuid():N}@sensus.com", "hash", true, DateTime.UtcNow).Value;
        _factory.UserRepository.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);
        IJwtTokenGenerator tokenGenerator = _factory.Services.GetRequiredService<IJwtTokenGenerator>();
        (string token, _) = tokenGenerator.GenerateToken(user);
        return (token, user);
    }

    private string CreateGuestToken()
    {
        IJwtTokenGenerator tokenGenerator = _factory.Services.GetRequiredService<IJwtTokenGenerator>();
        (string token, _) = tokenGenerator.GenerateGuestToken();
        return token;
    }

    private static Offer BuildOffer() =>
        Offer.Create(
            Guid.NewGuid(),
            "Pan artesanal",
            "Masa madre recién horneada",
            50m,
            20,
            5,
            DateTime.UtcNow.AddHours(-1),
            DateTime.UtcNow.AddDays(1),
            null,
            19.4326,
            -99.1332,
            DateTime.UtcNow,
            "Bakery").Value;

    [Fact]
    public async Task RegisterOfferView_AsGuest_ShouldReturnNoContentWithoutRecordingOpenInteraction()
    {
        Offer offer = BuildOffer();
        _factory.OfferRepository.GetByIdAsync(offer.Id, Arg.Any<CancellationToken>()).Returns(offer);
        _factory.OfferViewRepository.TryRegisterAsync(Arg.Any<OfferView>(), Arg.Any<CancellationToken>()).Returns(true);
        _factory.OfferRepository.IncrementViewCountAsync(offer.Id, Arg.Any<CancellationToken>()).Returns(true);

        using HttpClient client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateGuestToken());

        HttpResponseMessage response = await client.PostAsync($"/api/v1/offers/{offer.Id}/view", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        await _factory.OfferInteractionRecorder.DidNotReceiveWithAnyArgs()
            .RecordSingleAsync(default, default, null!, null!, null, default);
    }

    [Fact]
    public async Task RegisterOfferView_AndClaimOffer_AsRegisteredUser_ShouldRecordInteractions()
    {
        (string token, User user) = CreateRegisteredToken();
        Offer offer = BuildOffer();

        _factory.OfferRepository.GetByIdAsync(offer.Id, Arg.Any<CancellationToken>()).Returns(offer);
        _factory.OfferViewRepository.TryRegisterAsync(Arg.Any<OfferView>(), Arg.Any<CancellationToken>()).Returns(true);
        _factory.OfferRepository.IncrementViewCountAsync(offer.Id, Arg.Any<CancellationToken>()).Returns(true);
        _factory.OfferClaimRepository.GetActiveByUserAndOfferAsync(user.Id, offer.Id, Arg.Any<CancellationToken>())
            .Returns((OfferClaim?)null);
        _factory.OfferRepository.TryReserveStockAsync(offer.Id, Arg.Any<DateTime>(), Arg.Any<CancellationToken>()).Returns(true);
        _factory.OfferClaimRepository.TryAddAsync(Arg.Any<OfferClaim>(), Arg.Any<CancellationToken>()).Returns(true);

        using HttpClient client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        HttpResponseMessage viewRes = await client.PostAsync($"/api/v1/offers/{offer.Id}/view", content: null);
        viewRes.StatusCode.Should().Be(HttpStatusCode.NoContent);

        HttpResponseMessage claimRes = await client.PostAsync($"/api/v1/offers/{offer.Id}/claim", content: null);
        claimRes.StatusCode.Should().Be(HttpStatusCode.OK);

        await _factory.OfferInteractionRecorder.Received(1).RecordSingleAsync(
            user.Id,
            OfferInteractionType.Open,
            Arg.Is<OfferInteractionItem>(i => i.OfferId == offer.Id && i.Category == "Bakery"),
            "direct",
            null,
            Arg.Any<CancellationToken>());

        await _factory.OfferInteractionRecorder.Received(1).RecordSingleAsync(
            user.Id,
            OfferInteractionType.Claim,
            Arg.Is<OfferInteractionItem>(i => i.OfferId == offer.Id && i.Category == "Bakery"),
            "direct",
            null,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public void HmacUserPseudonymizer_ShouldBeDeterministicPerVersionAndSupportJsonRotation()
    {
        string key1 = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        string key2 = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        Guid userId = Guid.NewGuid();

        var pseudonymizer = new HmacUserPseudonymizer(Options.Create(new InteractionSettings
        {
            ActiveKeyVersion = 2,
            HmacKeysJson = JsonSerializer.Serialize(new Dictionary<string, string>
            {
                ["1"] = key1,
                ["2"] = key2
            })
        }));

        PseudonymizedUser first = pseudonymizer.Pseudonymize(userId);
        PseudonymizedUser second = pseudonymizer.Pseudonymize(userId);

        first.Should().Be(second);
        first.KeyVersion.Should().Be(2);
        first.PseudoUserId.Should().HaveLength(64);
        first.PseudoUserId.Should().NotContain(userId.ToString("N"));

        IReadOnlyList<PseudonymizedUser> allVersions = pseudonymizer.ComputeAllKnownPseudonyms(userId);
        allVersions.Should().HaveCount(2);
        allVersions.Select(v => v.KeyVersion).Should().Equal(1, 2);
        allVersions[0].PseudoUserId.Should().NotBe(allVersions[1].PseudoUserId);
    }

    [Fact]
    public async Task MongoOfferInteractionRecorder_WhenWriteFailsOrTimesOut_ShouldDegradeGracefullyAndRecordMetric()
    {
        string key1 = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var pseudonymizer = new HmacUserPseudonymizer(Options.Create(new InteractionSettings
        {
            ActiveKeyVersion = 1,
            HmacKeys = new Dictionary<int, string> { [1] = key1 }
        }));

        IOfferInteractionRepository repo = Substitute.For<IOfferInteractionRepository>();
        repo.InsertAsync(Arg.Any<OfferInteraction>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new TimeoutException("Atlas unavailable"));

        IInteractionMetricsPublisher metrics = Substitute.For<IInteractionMetricsPublisher>();
        IDateTimeProvider clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(Now);

        var recorder = new MongoOfferInteractionRecorder(
            pseudonymizer,
            repo,
            metrics,
            clock,
            Options.Create(new InteractionSettings { Enabled = true, WriteTimeoutMs = 50 }),
            NullLogger<MongoOfferInteractionRecorder>.Instance);

        List<OfferInteractionItem> twentyItems = Enumerable.Range(1, 20)
            .Select(i => OfferInteractionItem.Create(Guid.NewGuid(), i, FeedSlot.Ranked, "Food", DistanceBucket.Range1To3Km).Value)
            .ToList();

        Func<Task> act = () => recorder.RecordImpressionsAsync(Guid.NewGuid(), "snap-1", "feed-v1", 1, twentyItems);
        await act.Should().NotThrowAsync();

        metrics.Received(1).RecordWriteFailure(OfferInteractionType.Impression);
        metrics.DidNotReceiveWithAnyArgs().RecordWritten(default);
    }

    [Fact]
    public async Task MongoOfferInteractionRecorder_ShouldRespectImpressionSamplingWithoutDroppingOpensOrClaims()
    {
        string key1 = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var pseudonymizer = new HmacUserPseudonymizer(Options.Create(new InteractionSettings
        {
            ActiveKeyVersion = 1,
            HmacKeys = new Dictionary<int, string> { [1] = key1 }
        }));

        IOfferInteractionRepository repo = Substitute.For<IOfferInteractionRepository>();
        IInteractionMetricsPublisher metrics = Substitute.For<IInteractionMetricsPublisher>();
        IDateTimeProvider clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(Now);

        var recorder = new MongoOfferInteractionRecorder(
            pseudonymizer,
            repo,
            metrics,
            clock,
            Options.Create(new InteractionSettings { Enabled = true, ImpressionSampleRate = 0.0 }),
            NullLogger<MongoOfferInteractionRecorder>.Instance);

        OfferInteractionItem item = OfferInteractionItem.Create(Guid.NewGuid(), 1, FeedSlot.Ranked, "Food", DistanceBucket.Range0To1Km).Value;

        await recorder.RecordImpressionsAsync(Guid.NewGuid(), "snap-0", "feed-v1", 1, [item]);
        await recorder.RecordSingleAsync(Guid.NewGuid(), OfferInteractionType.Open, item);

        await repo.Received(1).InsertAsync(
            Arg.Is<OfferInteraction>(x => x.Type == OfferInteractionType.Open),
            Arg.Any<CancellationToken>());
        metrics.Received(1).RecordWritten(OfferInteractionType.Open);
    }

    [Fact]
    public async Task EmbeddedInteractionMetricsPublisher_AndAggregationService_ShouldEmitFreeTierEmfAndComputeRates()
    {
        using var writer = new StringWriter();
        IDateTimeProvider clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(Now);

        var interactionOpts = Options.Create(new InteractionSettings
        {
            CollectionLimitBytes = 1000,
            ClusterLimitBytes = 2000,
            AlarmThresholdPercent = 70,
            EmitDetailedCustomMetrics = false
        });

        var publisher = new EmbeddedInteractionMetricsPublisher(
            Options.Create(new MetricsSettings { Enabled = true, Namespace = "Sensus/Api" }),
            interactionOpts,
            clock,
            NullLogger<EmbeddedInteractionMetricsPublisher>.Instance,
            writer);

        IOfferInteractionRepository repo = Substitute.For<IOfferInteractionRepository>();
        repo.AggregateDailyQualityAsync(Now.AddDays(-1), Arg.Any<CancellationToken>())
            .Returns([new InteractionQualityAggregate("feed-v1", FeedSlot.Ranked, 100, 25, 5)]);
        repo.GetStorageSizeAsync(Arg.Any<CancellationToken>())
            .Returns(new InteractionCollectionStorageSize(750, 1000));

        var service = new InteractionMetricsAggregationService(repo, publisher, clock, interactionOpts);
        InteractionStorageStats stats = await service.PublishDailySnapshotAsync();

        stats.CollectionUtilizationPercent.Should().Be(75d);
        stats.ClusterUtilizationPercent.Should().Be(50d);
        stats.MaxUtilizationPercent.Should().Be(75d);

        string emfOutput = writer.ToString();
        emfOutput.Should().Contain(EmbeddedInteractionMetricsPublisher.StorageMaxPercentMetric);
        emfOutput.Should().NotContain("pseudoUserId");
        emfOutput.Should().NotContain("userId");
    }
}
