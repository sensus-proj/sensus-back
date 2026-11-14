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

public class NearbyOffersEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private static readonly DateTime Now = new(2026, 11, 13, 12, 0, 0, DateTimeKind.Utc);

    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public NearbyOffersEndpointsTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _factory.FakeClock.Set(DateTime.UtcNow);
        _factory.OfferRepository.ClearReceivedCalls();
    }

    private string CreateRegisteredToken()
    {
        var user = User.Create("Consumidor", $"consumidor.{Guid.NewGuid():N}@sensus.com", "hash", true, DateTime.UtcNow).Value;
        _factory.UserRepository.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);
        var tokenGenerator = _factory.Services.GetRequiredService<IJwtTokenGenerator>();
        var (token, _) = tokenGenerator.GenerateToken(user);
        return token;
    }

    private string CreateGuestToken()
    {
        var tokenGenerator = _factory.Services.GetRequiredService<IJwtTokenGenerator>();
        var (token, _) = tokenGenerator.GenerateGuestToken();
        return token;
    }

    private static NearbyOffersPage BuildPage()
    {
        Offer offer = Offer.Create(
            Guid.NewGuid(),
            "Combo familiar",
            "Combo con bebida y postre",
            100m,
            20,
            10,
            Now.AddHours(-1),
            Now.AddDays(1),
            null,
            -12.05,
            -77.04,
            Now).Value;

        return new NearbyOffersPage(
            [new NearbyOfferHit(offer, 350.25, "Restaurante Central", MerchantCategory.Restaurantes)],
            1);
    }

    [Fact]
    public async Task NearbyOffers_ShouldReturnUnauthorized_WhenNoTokenIsProvided()
    {
        var response = await _client.GetAsync("/api/v1/offers/nearby?lat=-12.05&lng=-77.04&radiusKm=3");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task NearbyOffers_ShouldReturnPagedOffers_ForRegisteredUser()
    {
        // Arrange
        _factory.OfferRepository.GetNearbyAsync(
                Arg.Any<double>(),
                Arg.Any<double>(),
                Arg.Any<int>(),
                Arg.Any<MerchantCategory?>(),
                Arg.Any<int>(),
                Arg.Any<int>(),
                Arg.Any<DateTime>(),
                Arg.Any<CancellationToken>())
            .Returns(BuildPage());

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            "/api/v1/offers/nearby?lat=-12.05&lng=-77.04&radiusKm=3&category=Restaurantes&page=1&pageSize=20");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", CreateRegisteredToken());

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await response.Content.ReadFromJsonAsync<PagedResult<NearbyOfferDto>>();
        page.Should().NotBeNull();
        page!.TotalCount.Should().Be(1);
        page.Items.Should().HaveCount(1);
        page.Items[0].DistanceMeters.Should().Be(350.25);

        await _factory.OfferRepository.Received(1).GetNearbyAsync(
            -12.05,
            -77.04,
            3000,
            MerchantCategory.Restaurantes,
            1,
            20,
            Arg.Any<DateTime>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NearbyOffers_ShouldClampRadius_ForGuest()
    {
        // Arrange
        _factory.OfferRepository.GetNearbyAsync(
                Arg.Any<double>(),
                Arg.Any<double>(),
                Arg.Any<int>(),
                Arg.Any<MerchantCategory?>(),
                Arg.Any<int>(),
                Arg.Any<int>(),
                Arg.Any<DateTime>(),
                Arg.Any<CancellationToken>())
            .Returns(new NearbyOffersPage([], 0));

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            "/api/v1/offers/nearby?lat=-12.05&lng=-77.04&radiusKm=5");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", CreateGuestToken());

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await _factory.OfferRepository.Received(1).GetNearbyAsync(
            Arg.Any<double>(),
            Arg.Any<double>(),
            2000,
            Arg.Any<MerchantCategory?>(),
            Arg.Any<int>(),
            Arg.Any<int>(),
            Arg.Any<DateTime>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NearbyOffers_ShouldReturnBadRequest_WhenRadiusIsOutOfRangeForRegisteredUser()
    {
        // Arrange
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            "/api/v1/offers/nearby?lat=-12.05&lng=-77.04&radiusKm=6");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", CreateRegisteredToken());

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await _factory.OfferRepository.DidNotReceiveWithAnyArgs().GetNearbyAsync(
            default,
            default,
            default,
            default,
            default,
            default,
            default,
            default);
    }

    [Fact]
    public async Task NearbyOffers_ShouldReturnBadRequest_WhenPageSizeExceedsMaximum()
    {
        // Arrange
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            "/api/v1/offers/nearby?lat=-12.05&lng=-77.04&radiusKm=3&pageSize=101");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", CreateRegisteredToken());

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
