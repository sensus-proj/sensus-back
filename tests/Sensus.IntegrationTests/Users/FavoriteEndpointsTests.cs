using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Sensus.Application.Common.Interfaces;
using Sensus.Domain.Entities;
using Sensus.Domain.Enums;
using Sensus.Domain.Models;
using Sensus.IntegrationTests.Infrastructure;

namespace Sensus.IntegrationTests.Users;

public sealed class FavoriteEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    // Reloj real: GenerateGuestToken usa el IDateTimeProvider inyectado, pero la validación JWT usa
    // DateTime.UtcNow. Fijarlo en el pasado haría que el token de invitado apareciera expirado (401).
    private static readonly DateTime Now = DateTime.UtcNow;

    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public FavoriteEndpointsTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _factory.OfferFavoriteRepository.ClearReceivedCalls();
    }

    [Fact]
    public async Task ToggleFavorite_ReturnsOk_WhenOfferIsFavorited()
    {
        (string token, _) = CreateRegisteredToken();
        Offer offer = ActiveOffer();
        _factory.OfferRepository.GetByIdAsync(offer.Id, Arg.Any<CancellationToken>()).Returns(offer);
        _factory.OfferFavoriteRepository
            .ToggleFavoriteAsync(Arg.Any<Guid>(), offer.Id, Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(true);
        _factory.OfferFavoriteRepository.CountByOfferIdAsync(offer.Id, Arg.Any<CancellationToken>()).Returns(1);

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        HttpResponseMessage response = await _client.PostAsync($"/api/v1/users/favorites/{offer.Id}", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        string body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("\"favorited\":true");
        body.Should().Contain("\"favoriteCount\":1");
    }

    [Fact]
    public async Task ToggleFavorite_ReturnsOk_WhenOfferIsUnfavorited()
    {
        (string token, _) = CreateRegisteredToken();
        Offer offer = ActiveOffer();
        _factory.OfferRepository.GetByIdAsync(offer.Id, Arg.Any<CancellationToken>()).Returns(offer);
        _factory.OfferFavoriteRepository
            .ToggleFavoriteAsync(Arg.Any<Guid>(), offer.Id, Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(false);
        _factory.OfferFavoriteRepository.CountByOfferIdAsync(offer.Id, Arg.Any<CancellationToken>()).Returns(0);

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        HttpResponseMessage response = await _client.PostAsync($"/api/v1/users/favorites/{offer.Id}", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        string body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("\"favorited\":false");
        body.Should().Contain("\"favoriteCount\":0");
    }

    [Fact]
    public async Task ToggleFavorite_ReturnsNotFound_WhenOfferIsArchived()
    {
        (string token, _) = CreateRegisteredToken();
        Offer offer = ArchivedOffer();
        _factory.OfferRepository.GetByIdAsync(offer.Id, Arg.Any<CancellationToken>()).Returns(offer);

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        HttpResponseMessage response = await _client.PostAsync($"/api/v1/users/favorites/{offer.Id}", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await _factory.OfferFavoriteRepository
            .DidNotReceiveWithAnyArgs()
            .ToggleFavoriteAsync(default, default, default, default);
    }

    [Fact]
    public async Task ToggleFavorite_ReturnsUnauthorized_WhenNoTokenProvided()
    {
        _client.DefaultRequestHeaders.Authorization = null;

        HttpResponseMessage response = await _client.PostAsync(
            $"/api/v1/users/favorites/{Guid.NewGuid()}",
            content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ToggleFavorite_ReturnsForbidden_WhenGuestTokenUsed()
    {
        string guestToken = _factory.Services.GetRequiredService<IJwtTokenGenerator>().GenerateGuestToken().Token;
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", guestToken);

        HttpResponseMessage response = await _client.PostAsync(
            $"/api/v1/users/favorites/{Guid.NewGuid()}",
            content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetFavorites_ReturnsOkPagedResult_ForRegisteredUser()
    {
        (string token, User user) = CreateRegisteredToken();
        Offer offer = ActiveOffer();
        var entry = new FavoriteOfferEntry(offer, "Cafetería Centro", MerchantCategory.Cafeterias, Now.AddMinutes(-10));
        _factory.OfferFavoriteRepository
            .GetPagedByUserIdAsync(user.Id, 1, 20, Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(new FavoriteOffersPage([entry], 1));

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        HttpResponseMessage response = await _client.GetAsync("/api/v1/users/favorites?page=1&pageSize=20");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        string body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("\"totalCount\":1");
        body.Should().Contain("\"pageNumber\":1");
        body.Should().Contain("\"pageSize\":20");
        body.Should().Contain("\"merchantName\":\"Cafetería Centro\"");
        body.Should().Contain("\"title\":\"Oferta activa\"");
    }

    [Fact]
    public async Task GetFavorites_ReturnsBadRequest_WhenPageSizeExceedsMaximum()
    {
        (string token, _) = CreateRegisteredToken();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        HttpResponseMessage response = await _client.GetAsync("/api/v1/users/favorites?page=1&pageSize=101");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetFavorites_ReturnsUnauthorizedAndForbidden_AccordingToSession()
    {
        _client.DefaultRequestHeaders.Authorization = null;
        HttpResponseMessage unauthResponse = await _client.GetAsync("/api/v1/users/favorites?page=1&pageSize=20");
        unauthResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        string guestToken = _factory.Services.GetRequiredService<IJwtTokenGenerator>().GenerateGuestToken().Token;
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", guestToken);
        HttpResponseMessage guestResponse = await _client.GetAsync("/api/v1/users/favorites?page=1&pageSize=20");
        guestResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private (string Token, User User) CreateRegisteredToken()
    {
        User user = User.Create(
            "Carlos Pérez",
            $"carlos.{Guid.NewGuid():N}@sensus.com",
            "hash",
            true,
            Now).Value;

        _factory.UserRepository.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);
        var tokenGenerator = _factory.Services.GetRequiredService<IJwtTokenGenerator>();
        return (tokenGenerator.GenerateToken(user).Token, user);
    }

    private static Offer ActiveOffer() => Offer.Create(
        Guid.NewGuid(),
        "Oferta activa",
        "Descripción de la oferta activa",
        100m,
        20,
        5,
        Now.AddDays(-1),
        Now.AddDays(1),
        category: "Food").Value;

    private static Offer ArchivedOffer() => Offer.Reconstitute(
        Guid.NewGuid(),
        Guid.NewGuid(),
        "Oferta archivada",
        "Descripción de la oferta archivada",
        100m,
        20,
        80m,
        5,
        Now.AddDays(-5),
        Now.AddDays(-1),
        OfferStatus.Archived,
        null,
        null,
        null,
        Now.AddDays(-10),
        category: "Food");
}
