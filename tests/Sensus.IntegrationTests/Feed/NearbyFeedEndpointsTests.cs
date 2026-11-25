using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Common.Models;
using Sensus.Application.Feed.DTOs;
using Sensus.Domain.Entities;
using Sensus.IntegrationTests.Infrastructure;

namespace Sensus.IntegrationTests.Feed;

/// <summary>
/// Spec 026: GET /api/v1/feed/nearby — éxito, entrada inválida, sin autenticación y sin permisos.
/// </summary>
public sealed class NearbyFeedEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public NearbyFeedEndpointsTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _factory.FeedPostRepository.ClearReceivedCalls();
        _factory.CacheService.GetAsync<NearbyFeedCacheEntry>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((NearbyFeedCacheEntry?)null);
    }

    private void AuthorizeRegistered()
    {
        User user = User.Create("Marta Díaz", $"marta.{Guid.NewGuid():N}@sensus.com", "hash", true, DateTime.UtcNow).Value;
        _factory.UserRepository.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);
        string token = _factory.Services.GetRequiredService<IJwtTokenGenerator>().GenerateToken(user).Token;
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    [Fact]
    public async Task GetNearby_Returns200WithPagedPublicProjection()
    {
        // CA-026-01
        AuthorizeRegistered();
        FeedPost post = FeedPost.Create(Guid.NewGuid(), "Ana", "Feria en el parque", null, DateTime.UtcNow, latitude: -12.05, longitude: -77.04).Value;
        _factory.FeedPostRepository.GetPublishedNearbyPagedAsync(-12.05, -77.04, 5000d, 1, 20, Arg.Any<CancellationToken>())
            .Returns((new List<FeedPost> { post }, 1));

        HttpResponseMessage response = await _client.GetAsync("/api/v1/feed/nearby?lat=-12.05&lng=-77.04&radiusKm=5");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        PagedResult<NearbyFeedPostDto>? page = await response.Content.ReadFromJsonAsync<PagedResult<NearbyFeedPostDto>>();
        page.Should().NotBeNull();
        page!.Items.Should().ContainSingle(i => i.Id == post.Id && i.Content == "Feria en el parque");
        page.TotalCount.Should().Be(1);
        page.PageNumber.Should().Be(1);
        page.PageSize.Should().Be(20);

        string raw = await response.Content.ReadAsStringAsync();
        raw.Should().NotContain("latitude").And.NotContain("longitude").And.NotContain("-77.04");
    }

    [Fact]
    public async Task GetNearby_DefaultsRadiusTo5Km()
    {
        AuthorizeRegistered();
        _factory.FeedPostRepository.GetPublishedNearbyPagedAsync(
                Arg.Any<double>(), Arg.Any<double>(), Arg.Any<double>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns((new List<FeedPost>(), 0));

        HttpResponseMessage response = await _client.GetAsync("/api/v1/feed/nearby?lat=19.4326&lng=-99.1332");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await _factory.FeedPostRepository.Received(1).GetPublishedNearbyPagedAsync(
            19.4326, -99.1332, 5000d, 1, 20, Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("lat=-12.05&lng=-77.04&radiusKm=5.01")]   // CA-026-05
    [InlineData("lat=-12.05&lng=-77.04&radiusKm=0")]
    [InlineData("lng=-77.04&radiusKm=5")]                  // sin lat
    [InlineData("lat=91&lng=-77.04")]
    [InlineData("lat=-12.05&lng=181")]
    [InlineData("lat=abc&lng=-77.04")]
    [InlineData("lat=NaN&lng=-77.04")]
    [InlineData("lat=-12.05&lng=-77.04&pageSize=101")]
    [InlineData("lat=-12.05&lng=-77.04&page=0")]
    public async Task GetNearby_Returns400_ForInvalidInput(string queryString)
    {
        AuthorizeRegistered();

        HttpResponseMessage response = await _client.GetAsync($"/api/v1/feed/nearby?{queryString}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await _factory.FeedPostRepository.DidNotReceive().GetPublishedNearbyPagedAsync(
            Arg.Any<double>(), Arg.Any<double>(), Arg.Any<double>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetNearby_Returns401_WithoutToken()
    {
        _client.DefaultRequestHeaders.Authorization = null;

        HttpResponseMessage response = await _client.GetAsync("/api/v1/feed/nearby?lat=-12.05&lng=-77.04");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetNearby_Returns403_ForGuestToken()
    {
        string guest = _factory.Services.GetRequiredService<IJwtTokenGenerator>().GenerateGuestToken().Token;
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", guest);

        HttpResponseMessage response = await _client.GetAsync("/api/v1/feed/nearby?lat=-12.05&lng=-77.04");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CreatePost_PersistsSharedLocation_AndRejectsIncompleteLocation()
    {
        AuthorizeRegistered();

        HttpResponseMessage created = await _client.PostAsJsonAsync(
            "/api/v1/feed/posts",
            new { content = "Mercado abierto hoy", latitude = -12.05, longitude = -77.04 });

        created.StatusCode.Should().Be(HttpStatusCode.Created);
        await _factory.FeedPostRepository.Received(1).AddAsync(
            Arg.Is<FeedPost>(p => p.Latitude == -12.05 && p.Longitude == -77.04),
            Arg.Any<CancellationToken>());

        HttpResponseMessage incomplete = await _client.PostAsJsonAsync(
            "/api/v1/feed/posts",
            new { content = "Solo latitud", latitude = -12.05 });

        incomplete.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
