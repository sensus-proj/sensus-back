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
using Sensus.IntegrationTests.Infrastructure;

namespace Sensus.IntegrationTests.Offers;

public class OfferDetailEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public OfferDetailEndpointsTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _factory.FakeClock.Set(DateTime.UtcNow);
        _factory.OfferRepository.ClearReceivedCalls();
        _factory.OfferViewRepository.ClearReceivedCalls();
        _factory.OfferClaimRepository.ClearReceivedCalls();
    }

    private (string Token, User User) CreateRegisteredToken()
    {
        User user = User.Create("Consumidor", $"consumidor.{Guid.NewGuid():N}@sensus.com", "hash", true, DateTime.UtcNow).Value;
        _factory.UserRepository.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);

        var tokenGenerator = _factory.Services.GetRequiredService<IJwtTokenGenerator>();
        var (token, _) = tokenGenerator.GenerateToken(user);
        return (token, user);
    }

    private static Offer BuildOffer() =>
        Offer.Create(
            Guid.NewGuid(),
            "Combo familiar",
            "Combo con bebida y postre",
            100m,
            20,
            10,
            DateTime.UtcNow.AddHours(-1),
            DateTime.UtcNow.AddDays(1),
            null,
            19.4326,
            -99.1332,
            DateTime.UtcNow).Value;

    private static Comercio BuildComercio(Guid merchantId) =>
        Comercio.Reconstitute(
            merchantId,
            Guid.NewGuid(),
            "Restaurante Central",
            "Calle Reforma 100",
            "06000",
            19.4326,
            -99.1332,
            activo: true,
            DateTime.UtcNow,
            MerchantCategory.Restaurantes);

    [Fact]
    public async Task GetOfferDetail_ShouldReturnUnauthorized_WhenNoTokenIsProvided()
    {
        HttpResponseMessage response = await _client.GetAsync($"/api/v1/offers/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetOfferDetail_ShouldReturnDetail_WhenOfferIsVisible()
    {
        // Arrange
        Offer offer = BuildOffer();
        _factory.OfferRepository.GetByIdAsync(offer.Id, Arg.Any<CancellationToken>()).Returns(offer);
        _factory.ComercioRepository.GetByIdAsync(offer.MerchantId, Arg.Any<CancellationToken>())
            .Returns(BuildComercio(offer.MerchantId));

        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/offers/{offer.Id}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", CreateRegisteredToken().Token);

        // Act
        HttpResponseMessage response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        OfferDetailDto? dto = await response.Content.ReadFromJsonAsync<OfferDetailDto>();
        dto.Should().NotBeNull();
        dto!.Id.Should().Be(offer.Id);
        dto.MerchantName.Should().Be("Restaurante Central");
        dto.Category.Should().Be(MerchantCategory.Restaurantes.ToString());
        dto.ViewCount.Should().Be(0);
        dto.HasActiveClaim.Should().BeFalse();
    }

    [Fact]
    public async Task GetOfferDetail_ShouldReturnNotFound_WhenOfferIsArchived()
    {
        // Arrange
        Offer offer = BuildOffer();
        offer.Archive(DateTime.UtcNow);
        _factory.OfferRepository.GetByIdAsync(offer.Id, Arg.Any<CancellationToken>()).Returns(offer);

        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/offers/{offer.Id}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", CreateRegisteredToken().Token);

        // Act
        HttpResponseMessage response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetOfferDetail_ShouldReturnNotFound_WhenOfferDoesNotExist()
    {
        // Arrange
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/offers/{Guid.NewGuid()}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", CreateRegisteredToken().Token);

        // Act
        HttpResponseMessage response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task RegisterOfferView_ShouldReturnNoContent_AndIncrementCounterOncePerDay()
    {
        // Arrange
        (string token, User user) = CreateRegisteredToken();
        Offer offer = BuildOffer();
        _factory.OfferRepository.GetByIdAsync(offer.Id, Arg.Any<CancellationToken>()).Returns(offer);
        _factory.OfferViewRepository
            .TryRegisterAsync(Arg.Any<OfferView>(), Arg.Any<CancellationToken>())
            .Returns(true, false);
        _factory.OfferRepository.IncrementViewCountAsync(offer.Id, Arg.Any<CancellationToken>()).Returns(true);

        using var firstRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/offers/{offer.Id}/view");
        firstRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        HttpResponseMessage firstResponse = await _client.SendAsync(firstRequest);

        using var secondRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/offers/{offer.Id}/view");
        secondRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        HttpResponseMessage secondResponse = await _client.SendAsync(secondRequest);

        // Assert
        firstResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
        secondResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        await _factory.OfferViewRepository.Received(2).TryRegisterAsync(
            Arg.Is<OfferView>(v => v.UserId == user.Id && v.OfferId == offer.Id),
            Arg.Any<CancellationToken>());
        await _factory.OfferRepository.Received(1).IncrementViewCountAsync(
            offer.Id,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RegisterOfferView_ShouldReturnNotFound_WhenOfferIsArchived()
    {
        // Arrange
        Offer offer = BuildOffer();
        offer.Archive(DateTime.UtcNow);
        _factory.OfferRepository.GetByIdAsync(offer.Id, Arg.Any<CancellationToken>()).Returns(offer);

        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/offers/{offer.Id}/view");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", CreateRegisteredToken().Token);

        // Act
        HttpResponseMessage response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await _factory.OfferViewRepository.DidNotReceiveWithAnyArgs().TryRegisterAsync(default!, default);
    }
}
