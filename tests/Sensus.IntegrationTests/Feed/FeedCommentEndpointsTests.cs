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

public sealed class FeedCommentEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public FeedCommentEndpointsTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _factory.FakeClock.Set(new DateTime(2026, 11, 21, 14, 0, 0, DateTimeKind.Utc));
        _factory.FeedCommentRepository.ClearReceivedCalls();
    }

    private (string Token, User User) CreateRegisteredToken()
    {
        User user = User.Create("Sofía López", $"sofia.{Guid.NewGuid():N}@sensus.com", "hash", true, DateTime.UtcNow).Value;
        _factory.UserRepository.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);
        var tokenGenerator = _factory.Services.GetRequiredService<IJwtTokenGenerator>();
        return (tokenGenerator.GenerateToken(user).Token, user);
    }

    [Fact]
    public async Task CreateComment_ReturnsCreated_AndGetComments_ReturnsAscendingPage()
    {
        (string token, User user) = CreateRegisteredToken();
        Guid postId = Guid.NewGuid();
        _factory.FeedPostVisibilityLookup.GetVisibilityAsync(postId, Arg.Any<CancellationToken>())
            .Returns(new FeedPostLookupResult(true, true, Guid.NewGuid()));

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        HttpResponseMessage postResp = await _client.PostAsJsonAsync(
            $"/api/v1/feed/posts/{postId}/comments",
            new { content = "Excelente descuento, gracias por avisar!" });

        postResp.StatusCode.Should().Be(HttpStatusCode.Created);
        FeedCommentDto? created = await postResp.Content.ReadFromJsonAsync<FeedCommentDto>();
        created.Should().NotBeNull();
        created!.AuthorName.Should().Be("Sofía López");
        created.Content.Should().Be("Excelente descuento, gracias por avisar!");

        FeedComment saved = FeedComment.Create(postId, user.Id, user.NombreCompleto, created.Content, _factory.FakeClock.UtcNow).Value;
        _factory.FeedCommentRepository.GetByPostIdPagedAsync(postId, 1, 20, Arg.Any<CancellationToken>()).Returns([saved]);
        _factory.FeedCommentRepository.CountByPostIdAsync(postId, Arg.Any<CancellationToken>()).Returns(1);

        HttpResponseMessage getResp = await _client.GetAsync($"/api/v1/feed/posts/{postId}/comments?page=1&pageSize=20");
        getResp.StatusCode.Should().Be(HttpStatusCode.OK);
        PagedResult<FeedCommentDto>? page = await getResp.Content.ReadFromJsonAsync<PagedResult<FeedCommentDto>>();
        page.Should().NotBeNull();
        page!.TotalCount.Should().Be(1);
        page.Items.Should().ContainSingle(i => i.Content == "Excelente descuento, gracias por avisar!");
    }

    [Fact]
    public async Task CreateComment_ReturnsForbidden_ForGuest_AndNotFound_WhenPostMissing()
    {
        string guestToken = _factory.Services.GetRequiredService<IJwtTokenGenerator>().GenerateGuestToken().Token;
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", guestToken);

        HttpResponseMessage guestResp = await _client.PostAsJsonAsync(
            $"/api/v1/feed/posts/{Guid.NewGuid()}/comments",
            new { content = "Hola" });
        guestResp.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        (string token, _) = CreateRegisteredToken();
        Guid missingPostId = Guid.NewGuid();
        _factory.FeedPostVisibilityLookup.GetVisibilityAsync(missingPostId, Arg.Any<CancellationToken>())
            .Returns(new FeedPostLookupResult(false, false, null));

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        HttpResponseMessage missingResp = await _client.PostAsJsonAsync(
            $"/api/v1/feed/posts/{missingPostId}/comments",
            new { content = "Hola" });
        missingResp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetComments_ReturnsBadRequest_WhenPageSizeExceedsMaximum()
    {
        (string token, _) = CreateRegisteredToken();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        HttpResponseMessage response = await _client.GetAsync($"/api/v1/feed/posts/{Guid.NewGuid()}/comments?page=1&pageSize=101");
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
