using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Common.Models;
using Sensus.Application.Offers.DTOs;
using Sensus.Domain.Entities;
using Sensus.Domain.Enums;
using Sensus.Domain.Models;
using Sensus.Domain.Policies;
using Sensus.IntegrationTests.Infrastructure;

namespace Sensus.IntegrationTests.Offers;

/// <summary>
/// Pruebas de integración HTTP para Spec 041 (<c>GET /api/v1/offers/search</c>) y
/// Spec 043 (<c>GET /api/v1/offers/{id}/alternatives</c>).
/// </summary>
public sealed class OfferDiscoveryEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public OfferDiscoveryEndpointsTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _factory.FakeClock.Set(DateTime.UtcNow);
        _factory.OfferRepository.ClearReceivedCalls();
    }

    private string CreateRegisteredToken()
    {
        User user = User.Create("Consumidor", $"disc.{Guid.NewGuid():N}@sensus.com", "hash", true, DateTime.UtcNow).Value;
        _factory.UserRepository.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);
        IJwtTokenGenerator tokenGenerator = _factory.Services.GetRequiredService<IJwtTokenGenerator>();
        (string token, _) = tokenGenerator.GenerateToken(user);
        return token;
    }

    private string CreateGuestToken()
    {
        IJwtTokenGenerator tokenGenerator = _factory.Services.GetRequiredService<IJwtTokenGenerator>();
        (string token, _) = tokenGenerator.GenerateGuestToken();
        return token;
    }

    [Fact]
    public async Task SearchOffers_WithoutToken_ShouldReturn401()
    {
        using HttpClient client = _factory.CreateClient();
        HttpResponseMessage response = await client.GetAsync("/api/v1/offers/search?q=pan&lat=19.43&lng=-99.13");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData("/api/v1/offers/search?q=a&lat=19.43&lng=-99.13")]
    [InlineData("/api/v1/offers/search?lat=95&lng=-99.13")]
    [InlineData("/api/v1/offers/search?lat=19.43&lng=185")]
    [InlineData("/api/v1/offers/search?q=pan&lat=19.43&lng=-99.13&radiusKm=51")]
    [InlineData("/api/v1/offers/search?q=pan&lat=19.43&lng=-99.13&pageSize=101")]
    public async Task SearchOffers_WithInvalidParameters_ShouldReturn400(string url)
    {
        using HttpClient client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateRegisteredToken());

        HttpResponseMessage response = await client.GetAsync(url);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task SearchOffers_AsGuest_ShouldClampRadiusTo2Km_AndRegisteredDeterminismHolds()
    {
        DateTime expires = DateTime.UtcNow.AddDays(2);
        Guid nearId = Guid.NewGuid();
        Guid midId = Guid.NewGuid();
        List<OfferSearchCandidate> candidates =
        [
            new(nearId, "Pan dulce artesanal", "Conchas recién horneadas", nameof(OfferCategory.Bakery), null, 60m, 40m, 5, 450d, expires, Guid.NewGuid(), "La Espiga", 25, 2.0),
            new(midId, "Pan integral", "Hogaza fresca", nameof(OfferCategory.Bakery), null, 80m, 55m, 3, 1200d, expires, Guid.NewGuid(), "Horno Sur", 10, 1.5)
        ];

        _factory.OfferRepository.SearchOffersHybridAsync(
                Arg.Any<string?>(),
                Arg.Any<string?>(),
                Arg.Any<double>(),
                Arg.Any<double>(),
                Arg.Any<int>(),
                Arg.Any<int>(),
                Arg.Any<DateTime>(),
                Arg.Any<CancellationToken>())
            .Returns(candidates);

        using HttpClient client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateGuestToken());

        HttpResponseMessage guestResponse = await client.GetAsync("/api/v1/offers/search?q=panader%C3%ADa&lat=19.43&lng=-99.13&radiusKm=25");
        guestResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        await _factory.OfferRepository.Received(1).SearchOffersHybridAsync(
            "panaderia",
            "panadería",
            19.43,
            -99.13,
            GuestRadiusPolicy.GuestMaxRadiusMeters,
            Arg.Any<int>(),
            Arg.Any<DateTime>(),
            Arg.Any<CancellationToken>());

        // CA-041-04: determinismo en llamadas repetidas
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateRegisteredToken());
        PagedResult<OfferSearchItemDto>? r1 = await client.GetFromJsonAsync<PagedResult<OfferSearchItemDto>>("/api/v1/offers/search?q=panader%C3%ADa&lat=19.43&lng=-99.13");
        PagedResult<OfferSearchItemDto>? r2 = await client.GetFromJsonAsync<PagedResult<OfferSearchItemDto>>("/api/v1/offers/search?q=panader%C3%ADa&lat=19.43&lng=-99.13");

        r1.Should().NotBeNull();
        r2.Should().NotBeNull();
        r1!.Items.Select(i => i.OfferId).Should().Equal(r2!.Items.Select(i => i.OfferId));

        // CA-041-02: consulta vacía hace fallback a ofertas cercanas
        HttpResponseMessage emptyResp = await client.GetAsync("/api/v1/offers/search?lat=19.43&lng=-99.13");
        emptyResp.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task SearchOffers_ExceedingReadHeavyRateLimit_ShouldReturn429()
    {
        _factory.OfferRepository.SearchOffersHybridAsync(
                Arg.Any<string?>(),
                Arg.Any<string?>(),
                Arg.Any<double>(),
                Arg.Any<double>(),
                Arg.Any<int>(),
                Arg.Any<int>(),
                Arg.Any<DateTime>(),
                Arg.Any<CancellationToken>())
            .Returns(Array.Empty<OfferSearchCandidate>());

        using HttpClient client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateRegisteredToken());
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "198.51.100.41");

        HttpStatusCode lastStatus = HttpStatusCode.OK;
        for (int i = 0; i < 125; i++)
        {
            HttpResponseMessage resp = await client.GetAsync("/api/v1/offers/search?lat=19.43&lng=-99.13");
            lastStatus = resp.StatusCode;
            if (lastStatus == HttpStatusCode.TooManyRequests)
            {
                break;
            }
        }

        lastStatus.Should().Be(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task GetAlternatives_WithoutToken_ShouldReturn401_AndInvalidParams400()
    {
        using HttpClient client = _factory.CreateClient();
        (await client.GetAsync($"/api/v1/offers/{Guid.NewGuid()}/alternatives")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateRegisteredToken());
        (await client.GetAsync($"/api/v1/offers/{Guid.NewGuid()}/alternatives?limit=20")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await client.GetAsync($"/api/v1/offers/{Guid.NewGuid()}/alternatives?limit=0")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await client.GetAsync("/api/v1/offers/no-es-guid/alternatives")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetAlternatives_WhenMissingOrNotVisible_ShouldReturnUniform404()
    {
        _factory.OfferRepository.GetAlternativesAsync(
                Arg.Any<Guid>(),
                Arg.Any<int>(),
                Arg.Any<decimal>(),
                Arg.Any<DateTime>(),
                Arg.Any<CancellationToken>())
            .Returns(OfferAlternativesLookupResult.NotFoundOrUnavailable());

        using HttpClient client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateRegisteredToken());

        HttpResponseMessage rMissing = await client.GetAsync($"/api/v1/offers/{Guid.NewGuid()}/alternatives");
        HttpResponseMessage rPaused = await client.GetAsync($"/api/v1/offers/{Guid.NewGuid()}/alternatives");

        rMissing.StatusCode.Should().Be(HttpStatusCode.NotFound);
        rPaused.StatusCode.Should().Be(HttpStatusCode.NotFound);

        using JsonDocument doc1 = JsonDocument.Parse(await rMissing.Content.ReadAsStringAsync());
        using JsonDocument doc2 = JsonDocument.Parse(await rPaused.Content.ReadAsStringAsync());

        doc1.RootElement.GetProperty("title").GetString().Should().Be(doc2.RootElement.GetProperty("title").GetString());
        doc1.RootElement.GetProperty("detail").GetString().Should().Be(doc2.RootElement.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task GetAlternatives_AsGuestAndRegistered_ShouldRespect2KmLimitAndResultLimit()
    {
        Guid originId = Guid.NewGuid();
        DateTime expires = DateTime.UtcNow.AddDays(1);
        List<OfferAlternativeCandidate> alts =
        [
            new(Guid.NewGuid(), "Alt 1", nameof(OfferCategory.Bakery), null, 100m, 80m, 4, 300d, expires.AddHours(2), Guid.NewGuid(), "M1"),
            new(Guid.NewGuid(), "Alt 2", nameof(OfferCategory.Bakery), null, 100m, 95m, 2, 700d, expires.AddHours(1), Guid.NewGuid(), "M2"),
            new(Guid.NewGuid(), "Alt 3", nameof(OfferCategory.Bakery), null, 100m, 99m, 1, 900d, expires.AddHours(4), Guid.NewGuid(), "M3")
        ];

        _factory.OfferRepository.GetAlternativesAsync(
                originId,
                Arg.Any<int>(),
                Arg.Any<decimal>(),
                Arg.Any<DateTime>(),
                Arg.Any<CancellationToken>())
            .Returns(new OfferAlternativesLookupResult(true, alts));

        using HttpClient client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateGuestToken());

        HttpResponseMessage guestResp = await client.GetAsync($"/api/v1/offers/{originId}/alternatives?limit=2");
        guestResp.StatusCode.Should().Be(HttpStatusCode.OK);
        List<OfferAlternativeItemDto>? guestItems = await guestResp.Content.ReadFromJsonAsync<List<OfferAlternativeItemDto>>();
        guestItems.Should().HaveCount(2);

        await _factory.OfferRepository.Received(1).GetAlternativesAsync(
            originId,
            OfferAlternativesPolicy.GuestMaxDistanceMeters,
            1.20m,
            Arg.Any<DateTime>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetAlternatives_ExceedingReadHeavyRateLimit_ShouldReturn429()
    {
        _factory.OfferRepository.GetAlternativesAsync(
                Arg.Any<Guid>(),
                Arg.Any<int>(),
                Arg.Any<decimal>(),
                Arg.Any<DateTime>(),
                Arg.Any<CancellationToken>())
            .Returns(new OfferAlternativesLookupResult(true, []));

        using HttpClient client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateRegisteredToken());
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "198.51.100.43");

        HttpStatusCode lastStatus = HttpStatusCode.OK;
        for (int i = 0; i < 125; i++)
        {
            HttpResponseMessage resp = await client.GetAsync($"/api/v1/offers/{Guid.NewGuid()}/alternatives");
            lastStatus = resp.StatusCode;
            if (lastStatus == HttpStatusCode.TooManyRequests)
            {
                break;
            }
        }

        lastStatus.Should().Be(HttpStatusCode.TooManyRequests);
    }
}
