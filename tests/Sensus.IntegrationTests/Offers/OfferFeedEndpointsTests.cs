using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Offers.DTOs;
using Sensus.Domain.Entities;
using Sensus.Domain.Enums;
using Sensus.Domain.Models;
using Sensus.IntegrationTests.Fixtures.OfferFeed;
using Sensus.IntegrationTests.Infrastructure;

namespace Sensus.IntegrationTests.Offers;

/// <summary>
/// Pruebas de integración HTTP del feed de promociones (Spec 042, T042-08 y T042-11 fixture §7.1).
/// </summary>
public sealed class OfferFeedEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly OfferFeedDeterministicFixture _fixture = new();

    public OfferFeedEndpointsTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _factory.FakeClock.Set(DateTime.UtcNow);
        _factory.OfferRepository.ClearReceivedCalls();
        _factory.UserInterestsRepository.ClearReceivedCalls();
        _factory.UserAffinityReader.ClearReceivedCalls();
        _factory.FeedSnapshotStore.ClearReceivedCalls();
        _factory.OfferInteractionRecorder.ClearReceivedCalls();
    }

    private (string Token, User User) CreateRegisteredToken(Guid? fixedUserId = null)
    {
        User user = User.Create("Consumidor", $"feed.{Guid.NewGuid():N}@sensus.com", "hash", true, DateTime.UtcNow).Value;
        if (fixedUserId.HasValue)
        {
            typeof(User).GetProperty("Id")!.SetValue(user, fixedUserId.Value);
        }

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

    [Fact]
    public async Task GetFeedOffers_WithoutToken_ShouldReturn401_AndAsGuest_ShouldReturn403()
    {
        using HttpClient client = _factory.CreateClient();
        string baseUrl = $"/api/v1/feed/offers?lat={OfferFeedDeterministicFixture.ReferenceLat.ToString(CultureInfo.InvariantCulture)}&lng={OfferFeedDeterministicFixture.ReferenceLng.ToString(CultureInfo.InvariantCulture)}";

        (await client.GetAsync(baseUrl)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateGuestToken());
        (await client.GetAsync(baseUrl)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Theory]
    [InlineData("/api/v1/feed/offers")]
    [InlineData("/api/v1/feed/offers?lat=95&lng=-99.1")]
    [InlineData("/api/v1/feed/offers?lat=19.4&lng=185")]
    [InlineData("/api/v1/feed/offers?lat=19.4&lng=-99.1&radiusKm=0")]
    [InlineData("/api/v1/feed/offers?lat=19.4&lng=-99.1&radiusKm=51")]
    [InlineData("/api/v1/feed/offers?lat=19.4&lng=-99.1&pageSize=101")]
    [InlineData("/api/v1/feed/offers?lat=19.4&lng=-99.1&snapshotId=no-es-guid")]
    public async Task GetFeedOffers_WithInvalidParameters_ShouldReturn400(string url)
    {
        (string token, _) = CreateRegisteredToken();
        using HttpClient client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        HttpResponseMessage response = await client.GetAsync(url);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetFeedOffers_WithDeterministicFixture_ShouldSatisfyAllPlan71AssertionsAndStablePagination()
    {
        DateTime now = OfferFeedDeterministicFixture.FixedNowUtc;

        // Genera tokens con FakeClock en DateTime.UtcNow (constructor) para respetar JwtBearer nbf del host,
        // luego fija FakeClock en FixedNowUtc para determinismo exacto del ranking.
        (string tokenA, _) = CreateRegisteredToken(OfferFeedDeterministicFixture.UserAWithInterestsId);
        (string tokenC, _) = CreateRegisteredToken(OfferFeedDeterministicFixture.UserCNoSignalsId);
        (string tokenE, _) = CreateRegisteredToken(OfferFeedDeterministicFixture.UserEMerchantOwnerId);
        _factory.FakeClock.Set(now);

        _factory.OfferRepository.GetFeedCandidatesAsync(
                Arg.Any<double>(), Arg.Any<double>(), Arg.Any<int>(), Arg.Any<Guid>(), Arg.Any<int>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                Guid reqUserId = call.ArgAt<Guid>(3);
                int radiusMeters = call.ArgAt<int>(2);
                return _fixture.QueryVisibleForUser(reqUserId, radiusMeters);
            });

        _factory.OfferRepository.GetFeedCandidatesByIdsAsync(
                Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<double>(), Arg.Any<double>(), Arg.Any<Guid>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                HashSet<Guid> ids = call.ArgAt<IReadOnlyCollection<Guid>>(0).ToHashSet();
                Guid reqUserId = call.ArgAt<Guid>(3);
                return _fixture.QueryVisibleForUser(reqUserId, 50_000)
                    .Where(c => ids.Contains(c.OfferId))
                    .ToList();
            });

        // Almacén en memoria de instantáneas simulando Redis
        var snapshots = new ConcurrentDictionary<(Guid, Guid), FeedSnapshot>();
        _factory.FeedSnapshotStore.SaveAsync(Arg.Any<Guid>(), Arg.Any<FeedSnapshot>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                snapshots[(call.ArgAt<Guid>(0), call.ArgAt<FeedSnapshot>(1).SnapshotId)] = call.ArgAt<FeedSnapshot>(1);
                return true;
            });
        _factory.FeedSnapshotStore.GetAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(call => snapshots.GetValueOrDefault((call.ArgAt<Guid>(0), call.ArgAt<Guid>(1))));

        string baseCoords = $"lat={OfferFeedDeterministicFixture.ReferenceLat.ToString(CultureInfo.InvariantCulture)}&lng={OfferFeedDeterministicFixture.ReferenceLng.ToString(CultureInfo.InvariantCulture)}";

        // Usuario (a): 2 intereses declarados (Bakery, Dairy)
        _factory.UserInterestsRepository.GetByUserIdAsync(OfferFeedDeterministicFixture.UserAWithInterestsId, Arg.Any<CancellationToken>())
            .Returns(UserInterests.Create(OfferFeedDeterministicFixture.UserAWithInterestsId, [OfferCategory.Bakery, OfferCategory.Dairy], now).Value);

        using HttpClient clientA = _factory.CreateClient();
        clientA.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenA);

        OfferFeedPageDto? page1 = await clientA.GetFromJsonAsync<OfferFeedPageDto>($"/api/v1/feed/offers?{baseCoords}&radiusKm=10&page=1&pageSize=20");
        page1.Should().NotBeNull();
        page1!.Items.Should().HaveCount(20);
        page1.SnapshotId.Should().NotBeNull();
        page1.RankingVersion.Should().Be("feed-v1");

        // Aserción §7.1: ≥ 60 % de intereses en el top 10 de (a)
        int top10Matches = page1.Items.Take(10).Count(i => i.Category is "Bakery" or "Dairy");
        top10Matches.Should().BeGreaterThanOrEqualTo(6);

        // Aserción §7.1: exploración en posiciones 5, 10, 15 y 20
        page1.Items[4].Slot.Should().Be("exploration");
        page1.Items[9].Slot.Should().Be("exploration");
        page1.Items[14].Slot.Should().Be("exploration");
        page1.Items[19].Slot.Should().Be("exploration");

        // Aserción §7.1: 0 ofertas excluidas
        HashSet<Guid> excludedIds = _fixture.ExcludedOffers.Select(o => o.Id).ToHashSet();
        page1.Items.Select(i => i.OfferId).Should().NotIntersectWith(excludedIds);

        // Aserción §7.1: máximo 3 consecutivas de la misma categoría y paginación estable sin repetidos (CA-042-05)
        OfferFeedPageDto? page2 = await clientA.GetFromJsonAsync<OfferFeedPageDto>($"/api/v1/feed/offers?{baseCoords}&radiusKm=10&page=2&pageSize=20&snapshotId={page1.SnapshotId}");
        page2.Should().NotBeNull();
        page2!.Items.Should().HaveCount(20);
        page1.Items.Select(i => i.OfferId).Intersect(page2.Items.Select(i => i.OfferId)).Should().BeEmpty();

        var combined40Categories = page1.Items.Concat(page2.Items).Select(i => i.Category).ToList();
        AssertMaxConsecutive(combined40Categories, 3);

        // Aserción §7.1: orden idéntico en dos ejecuciones
        OfferFeedPageDto? secondRun = await clientA.GetFromJsonAsync<OfferFeedPageDto>($"/api/v1/feed/offers?{baseCoords}&radiusKm=10&page=1&pageSize=20");
        secondRun!.Items.Select(i => i.OfferId).Should().Equal(page1.Items.Select(i => i.OfferId));

        // Impresiones emitidas hacia Spec 055 (FR-042-11)
        await _factory.OfferInteractionRecorder.Received().RecordImpressionsAsync(
            OfferFeedDeterministicFixture.UserAWithInterestsId,
            page1.SnapshotId!.Value.ToString(),
            "feed-v1",
            1,
            Arg.Is<IReadOnlyList<OfferInteractionItem>>(items => items.Count == 20),
            Arg.Any<CancellationToken>());

        // Usuario (c): sin intereses ni señales -> ordenado por cercanía y urgencia (CA-042-04)
        _factory.UserInterestsRepository.GetByUserIdAsync(OfferFeedDeterministicFixture.UserCNoSignalsId, Arg.Any<CancellationToken>())
            .Returns((UserInterests?)null);
        using HttpClient clientC = _factory.CreateClient();
        clientC.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenC);
        OfferFeedPageDto? pageC = await clientC.GetFromJsonAsync<OfferFeedPageDto>($"/api/v1/feed/offers?{baseCoords}&radiusKm=10&page=1&pageSize=20");
        pageC!.Items.Should().HaveCount(20);
        // Todas las ofertas del anillo más cercano (~333 m) aparecen antes que anillos lejanos (~8 km)
        pageC.Items[0].DistanceMeters.Should().BeLessThanOrEqualTo(pageC.Items[^1].DistanceMeters);

        // Usuario (e): comerciante -> sin sus propias ofertas (FR-042-05)
        using HttpClient clientE = _factory.CreateClient();
        clientE.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenE);
        OfferFeedPageDto? pageE = await clientE.GetFromJsonAsync<OfferFeedPageDto>($"/api/v1/feed/offers?{baseCoords}&radiusKm=10&page=1&pageSize=50");
        pageE!.Items.Should().NotContain(i => i.MerchantId == _fixture.UserEOwnMerchant.Id);
    }

    [Fact]
    public async Task GetFeedOffers_WhenRateLimitExceeded_ShouldReturn429()
    {
        // Política read-heavy en appsettings.json = 120 solicitudes/minuto (FR-042-14)
        (string token, User user) = CreateRegisteredToken();
        _factory.OfferRepository.GetFeedCandidatesAsync(
                Arg.Any<double>(), Arg.Any<double>(), Arg.Any<int>(), user.Id, Arg.Any<int>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<FeedOfferCandidate>());

        using HttpClient client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "198.51.100.88");

        string url = "/api/v1/feed/offers?lat=19.4326&lng=-99.1332";
        for (int i = 0; i < 120; i++)
        {
            HttpResponseMessage ok = await client.GetAsync(url);
            ok.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        HttpResponseMessage limited = await client.GetAsync(url);
        limited.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }

    private static void AssertMaxConsecutive(IReadOnlyList<string> categories, int limit)
    {
        int run = 0;
        string? prev = null;
        foreach (string cat in categories)
        {
            run = string.Equals(cat, prev, StringComparison.OrdinalIgnoreCase) ? run + 1 : 1;
            prev = cat;
            run.Should().BeLessThanOrEqualTo(limit);
        }
    }
}
