using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Feed.DTOs;
using Sensus.Domain.Entities;
using Sensus.IntegrationTests.Infrastructure;

namespace Sensus.IntegrationTests.Feed;

/// <summary>
/// Spec 039: POST /api/v1/feed/posts/{id}/like — éxito, entrada inválida, sin autenticación, sin permisos,
/// publicación no visible y límite de solicitudes.
/// </summary>
public sealed class FeedPostLikeEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public FeedPostLikeEndpointsTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _factory.FeedPostLikeRepository.ClearReceivedCalls();
    }

    private string CreateRegisteredToken(out Guid userId)
    {
        User user = User.Create(
            "Lucía Ramos",
            $"lucia.{Guid.NewGuid():N}@sensus.com",
            "hash",
            true,
            DateTime.UtcNow).Value;

        userId = user.Id;
        _factory.UserRepository.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);
        return _factory.Services.GetRequiredService<IJwtTokenGenerator>().GenerateToken(user).Token;
    }

    private Guid VisiblePost()
    {
        Guid postId = Guid.NewGuid();
        _factory.FeedPostVisibilityLookup.GetVisibilityAsync(postId, Arg.Any<CancellationToken>())
            .Returns(new FeedPostLookupResult(Exists: true, IsVisible: true, AuthorUserId: Guid.NewGuid()));
        return postId;
    }

    private void Authorize(string token) =>
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

    [Fact]
    public async Task Like_Returns200WithLikedTrueAndIncrementedCount()
    {
        // CA-039-01
        Authorize(CreateRegisteredToken(out Guid userId));
        Guid postId = VisiblePost();
        _factory.FeedPostLikeRepository.AddAsync(
                Arg.Is<FeedPostLike>(l => l.PostId == postId && l.UserId == userId), Arg.Any<CancellationToken>())
            .Returns(true);
        _factory.FeedPostLikeRepository.CountByPostIdAsync(postId, Arg.Any<CancellationToken>()).Returns(1);

        HttpResponseMessage response = await _client.PostAsJsonAsync($"/api/v1/feed/posts/{postId}/like", new { liked = true });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        FeedPostLikeStatusDto? body = await response.Content.ReadFromJsonAsync<FeedPostLikeStatusDto>();
        body.Should().Be(new FeedPostLikeStatusDto(true, 1));

        string raw = await response.Content.ReadAsStringAsync();
        raw.Should().Contain("\"liked\":true").And.Contain("\"likeCount\":1");
        raw.Should().NotContain(userId.ToString(), "la respuesta no expone quién reaccionó");
    }

    [Fact]
    public async Task Unlike_Returns200WithLikedFalseAndDecrementedCount()
    {
        // CA-039-02
        Authorize(CreateRegisteredToken(out Guid userId));
        Guid postId = VisiblePost();
        _factory.FeedPostLikeRepository.RemoveAsync(postId, userId, Arg.Any<CancellationToken>()).Returns(true);
        _factory.FeedPostLikeRepository.CountByPostIdAsync(postId, Arg.Any<CancellationToken>()).Returns(0);

        HttpResponseMessage response = await _client.PostAsJsonAsync($"/api/v1/feed/posts/{postId}/like", new { liked = false });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<FeedPostLikeStatusDto>()).Should().Be(new FeedPostLikeStatusDto(false, 0));
        await _factory.FeedPostLikeRepository.Received(1).RemoveAsync(postId, userId, Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, false)]
    public async Task Like_Returns404_WhenPostIsQuarantinedOrMissing(bool exists, bool visible)
    {
        // CA-039-03 / FR-039-03
        Authorize(CreateRegisteredToken(out _));
        Guid postId = Guid.NewGuid();
        _factory.FeedPostVisibilityLookup.GetVisibilityAsync(postId, Arg.Any<CancellationToken>())
            .Returns(new FeedPostLookupResult(exists, visible, null));

        HttpResponseMessage response = await _client.PostAsJsonAsync($"/api/v1/feed/posts/{postId}/like", new { liked = true });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await _factory.FeedPostLikeRepository.DidNotReceive().AddAsync(
            Arg.Is<FeedPostLike>(l => l.PostId == postId), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Like_Returns400_WhenLikedIsMissingOrNotBoolean()
    {
        Authorize(CreateRegisteredToken(out _));
        Guid postId = VisiblePost();

        HttpResponseMessage missing = await _client.PostAsJsonAsync($"/api/v1/feed/posts/{postId}/like", new { });
        missing.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        HttpResponseMessage wrongType = await _client.PostAsync(
            $"/api/v1/feed/posts/{postId}/like",
            new StringContent("{\"liked\":\"yes\"}", System.Text.Encoding.UTF8, "application/json"));
        wrongType.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Like_Returns404_WhenIdIsNotAGuid()
    {
        Authorize(CreateRegisteredToken(out _));

        HttpResponseMessage response = await _client.PostAsJsonAsync("/api/v1/feed/posts/not-a-guid/like", new { liked = true });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Like_Returns401_WithoutToken()
    {
        _client.DefaultRequestHeaders.Authorization = null;

        HttpResponseMessage response = await _client.PostAsJsonAsync($"/api/v1/feed/posts/{Guid.NewGuid()}/like", new { liked = true });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Like_Returns403_ForGuestToken()
    {
        Authorize(_factory.Services.GetRequiredService<IJwtTokenGenerator>().GenerateGuestToken().Token);

        HttpResponseMessage response = await _client.PostAsJsonAsync($"/api/v1/feed/posts/{VisiblePost()}/like", new { liked = true });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Like_Returns429_AfterSixtyTogglesPerUserAndHour()
    {
        // FR-039-06: umbral por defecto en configuración (RateLimiting:PostLikes = 60/3600 s).
        Authorize(CreateRegisteredToken(out _));
        Guid postId = VisiblePost();
        _factory.FeedPostLikeRepository.CountByPostIdAsync(postId, Arg.Any<CancellationToken>()).Returns(0);

        for (int i = 0; i < 60; i++)
        {
            HttpResponseMessage ok = await _client.PostAsJsonAsync($"/api/v1/feed/posts/{postId}/like", new { liked = i % 2 == 0 });
            ok.StatusCode.Should().Be(HttpStatusCode.OK, $"la solicitud {i + 1} está dentro del límite");
        }

        HttpResponseMessage limited = await _client.PostAsJsonAsync($"/api/v1/feed/posts/{postId}/like", new { liked = true });

        limited.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        limited.Headers.Contains("Retry-After").Should().BeTrue();
    }
}
