using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Common.Models;
using Sensus.Application.Offers.DTOs;
using Sensus.Domain.Entities;
using Sensus.Domain.Enums;
using Sensus.Domain.Models;
using Sensus.IntegrationTests.Infrastructure;

namespace Sensus.IntegrationTests.Offers;

public sealed class MerchantInventoryEndpointTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public MerchantInventoryEndpointTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetMyOffers_ShouldReturnPagedInventoryWithMetrics_WhenMerchantIsAuthenticated()
    {
        (_, Comercio comercio, string token) = CreateAuthenticatedMerchant();
        var offer = BuildOffer(comercio.Id, "Oferta con métricas", viewCount: 12);
        SetupPage(comercio.Id, null, 1, 20, [offer], 1);
        SetupClaimCounts(offer.Id, new OfferClaimCounts(ActiveClaims: 2, Redemptions: 3));
        SetupLikes(offer.Id, likesCount: 5, likedByMe: true);

        using var request = AuthorizedGet(token, "/api/v1/merchants/my-offers");
        HttpResponseMessage response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        PagedResult<MerchantOfferDto>? body = await response.Content.ReadFromJsonAsync<PagedResult<MerchantOfferDto>>();
        body.Should().NotBeNull();
        body!.TotalCount.Should().Be(1);
        body.PageNumber.Should().Be(1);
        body.PageSize.Should().Be(20);
        MerchantOfferDto item = body.Items.Should().ContainSingle().Subject;
        item.Id.Should().Be(offer.Id);
        item.ViewsCount.Should().Be(12);
        item.ActiveClaimsCount.Should().Be(2);
        item.RedemptionsCount.Should().Be(3);
        item.LikesCount.Should().Be(5);
        item.IsLikedByMe.Should().BeTrue();
    }

    [Fact]
    public async Task GetMyOffers_ShouldReturnOnlyArchivedOffers_WhenStatusFilterIsApplied()
    {
        (_, Comercio comercio, string token) = CreateAuthenticatedMerchant();
        var archived = BuildOffer(comercio.Id, "Archivada", OfferStatus.Archived);
        SetupPage(comercio.Id, OfferStatus.Archived, 2, 10, [archived], 11);
        SetupClaimCounts(archived.Id, OfferClaimCounts.Empty);

        using var request = AuthorizedGet(token, "/api/v1/merchants/my-offers?status=Archived&page=2&pageSize=10");
        HttpResponseMessage response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        PagedResult<MerchantOfferDto>? body = await response.Content.ReadFromJsonAsync<PagedResult<MerchantOfferDto>>();
        body.Should().NotBeNull();
        body!.Items.Should().ContainSingle().Which.Status.Should().Be("Archived");
        body.TotalCount.Should().Be(11);
        body.PageNumber.Should().Be(2);
        body.PageSize.Should().Be(10);
        await _factory.OfferRepository.Received(1).GetByMerchantIdPagedAsync(
            comercio.Id,
            OfferStatus.Archived,
            2,
            10,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetMyOffers_ShouldReturnEmptyPage_WhenMerchantHasNoOffers()
    {
        (_, Comercio comercio, string token) = CreateAuthenticatedMerchant();
        SetupPage(comercio.Id, null, 1, 20, [], 0);

        using var request = AuthorizedGet(token, "/api/v1/merchants/my-offers");
        HttpResponseMessage response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        PagedResult<MerchantOfferDto>? body = await response.Content.ReadFromJsonAsync<PagedResult<MerchantOfferDto>>();
        body.Should().NotBeNull();
        body!.Items.Should().BeEmpty();
        body.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task GetMyOffers_ShouldReturnBadRequest_WhenPageSizeExceedsMaximum()
    {
        (_, _, string token) = CreateAuthenticatedMerchant();

        using var request = AuthorizedGet(token, "/api/v1/merchants/my-offers?pageSize=200");
        HttpResponseMessage response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        string body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("tamaño de página");
    }

    [Fact]
    public async Task GetMyOffers_ShouldReturnBadRequest_WhenStatusIsInvalid()
    {
        (_, _, string token) = CreateAuthenticatedMerchant();

        using var request = AuthorizedGet(token, "/api/v1/merchants/my-offers?status=NoExiste");
        HttpResponseMessage response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        string body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("estado");
    }

    [Fact]
    public async Task GetMyOffers_ShouldReturnUnauthorized_WhenNoTokenIsProvided()
    {
        HttpResponseMessage response = await _client.GetAsync("/api/v1/merchants/my-offers");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetMyOffers_ShouldReturnForbidden_WhenUserIsNotMerchant()
    {
        var consumer = User.Create(
            "Consumidor",
            $"consumer-{Guid.NewGuid():N}@sensus.com",
            "hashed-pw",
            true,
            DateTime.UtcNow).Value;
        _factory.UserRepository.GetByIdAsync(consumer.Id, Arg.Any<CancellationToken>()).Returns(consumer);
        var (token, _) = _factory.Services.GetRequiredService<IJwtTokenGenerator>().GenerateToken(consumer);

        using var request = AuthorizedGet(token, "/api/v1/merchants/my-offers");
        HttpResponseMessage response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetMyOffers_ShouldNeverReturnOffersFromAnotherMerchant()
    {
        (_, Comercio ownStore, string token) = CreateAuthenticatedMerchant();
        Comercio otherStore = Comercio.Create(Guid.NewGuid(), "Otro comercio", "Calle 999", "06000", 19.5, -99.2).Value;
        var ownOffer = BuildOffer(ownStore.Id, "Oferta propia");
        SetupPage(ownStore.Id, null, 1, 20, [ownOffer], 1);
        SetupPage(otherStore.Id, null, 1, 20, [BuildOffer(otherStore.Id, "Oferta ajena")], 1);
        SetupClaimCounts(ownOffer.Id, OfferClaimCounts.Empty);

        using var request = AuthorizedGet(token, "/api/v1/merchants/my-offers");
        HttpResponseMessage response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        PagedResult<MerchantOfferDto>? body = await response.Content.ReadFromJsonAsync<PagedResult<MerchantOfferDto>>();
        body.Should().NotBeNull();
        body!.Items.Should().ContainSingle().Which.MerchantId.Should().Be(ownStore.Id);
        await _factory.OfferRepository.Received(1).GetByMerchantIdPagedAsync(
            ownStore.Id, null, 1, 20, Arg.Any<CancellationToken>());
        await _factory.OfferRepository.DidNotReceive().GetByMerchantIdPagedAsync(
            otherStore.Id,
            Arg.Any<OfferStatus?>(),
            Arg.Any<int>(),
            Arg.Any<int>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetMyOffers_ShouldMeetLatencyTarget_ForTwentyRequests()
    {
        (_, Comercio comercio, string token) = CreateAuthenticatedMerchant();
        var offer = BuildOffer(comercio.Id, "Oferta latencia");
        SetupPage(comercio.Id, null, 1, 20, [offer], 1);
        SetupClaimCounts(offer.Id, OfferClaimCounts.Empty);
        List<double> latencies = [];

        for (int i = 0; i < 20; i++)
        {
            using var request = AuthorizedGet(token, "/api/v1/merchants/my-offers");
            var stopwatch = Stopwatch.StartNew();
            HttpResponseMessage response = await _client.SendAsync(request);
            stopwatch.Stop();

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            latencies.Add(stopwatch.Elapsed.TotalMilliseconds);
        }

        latencies.Sort();
        int p95Index = (int)Math.Ceiling(latencies.Count * 0.95) - 1;
        latencies[p95Index].Should().BeLessThan(600);
    }

    private (User User, Comercio Comercio, string Token) CreateAuthenticatedMerchant()
    {
        var user = User.Create(
            "Comerciante Inventario",
            $"inventory-{Guid.NewGuid():N}@sensus.com",
            "hashed-pw",
            true,
            DateTime.UtcNow).Value;
        user.AddRole(RoleType.Comerciante);
        _factory.UserRepository.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);

        var comercio = Comercio.Create(user.Id, "Tienda Inventario", "Calle 1", "06000", 19.4, -99.1).Value;
        _factory.ComercioRepository.GetByUsuarioIdAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Comercio?>(comercio));

        var (token, _) = _factory.Services.GetRequiredService<IJwtTokenGenerator>().GenerateToken(user);
        return (user, comercio, token);
    }

    private void SetupPage(
        Guid merchantId,
        OfferStatus? status,
        int page,
        int pageSize,
        IReadOnlyList<Offer> offers,
        int totalCount) =>
        _factory.OfferRepository
            .GetByMerchantIdPagedAsync(merchantId, status, page, pageSize, Arg.Any<CancellationToken>())
            .Returns((offers, totalCount));

    private void SetupClaimCounts(Guid offerId, OfferClaimCounts counts) =>
        _factory.OfferClaimRepository
            .CountByOfferIdsAsync(Arg.Any<IEnumerable<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<Guid, OfferClaimCounts> { [offerId] = counts });

    private void SetupLikes(Guid offerId, int likesCount, bool likedByMe)
    {
        _factory.OfferLikeRepository
            .CountByOfferIdsAsync(Arg.Any<IEnumerable<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<Guid, int> { [offerId] = likesCount });
        _factory.OfferLikeRepository
            .GetLikedOfferIdsAsync(Arg.Any<Guid>(), Arg.Any<IEnumerable<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(likedByMe ? new HashSet<Guid> { offerId } : new HashSet<Guid>());
    }

    private static HttpRequestMessage AuthorizedGet(string token, string url)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    private static Offer BuildOffer(
        Guid merchantId,
        string title,
        OfferStatus status = OfferStatus.Published,
        int viewCount = 0)
    {
        var now = new DateTime(2026, 11, 20, 10, 0, 0, DateTimeKind.Utc);
        return Offer.Reconstitute(
            Guid.NewGuid(),
            merchantId,
            title,
            "Descripción de prueba",
            100m,
            10,
            90m,
            5,
            now.AddHours(1),
            now.AddDays(2),
            status,
            null,
            null,
            null,
            now,
            viewCount: viewCount);
    }
}
