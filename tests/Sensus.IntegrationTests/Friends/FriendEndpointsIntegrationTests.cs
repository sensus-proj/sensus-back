using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Friends.DTOs;
using Sensus.Domain.Entities;
using Sensus.Domain.Enums;
using Sensus.IntegrationTests.Infrastructure;

namespace Sensus.IntegrationTests.Friends;

public sealed class FriendEndpointsIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public FriendEndpointsIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _factory.FakeClock.Set(new DateTime(2026, 11, 21, 17, 0, 0, DateTimeKind.Utc));
        _factory.FriendRequestRepository.ClearReceivedCalls();
        _factory.SharedOfferRepository.ClearReceivedCalls();
    }

    private (string Token, User User) CreateRegisteredToken(string name = "Elena Ortiz")
    {
        User user = User.Create(name, $"user.{Guid.NewGuid():N}@sensus.com", "hash", true, DateTime.UtcNow).Value;
        _factory.UserRepository.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);
        string token = _factory.Services.GetRequiredService<IJwtTokenGenerator>().GenerateToken(user).Token;
        return (token, user);
    }

    [Fact]
    public async Task GetFriendsOverview_ReturnsUnauthorized_WithoutToken_AndForbidden_ForGuest()
    {
        HttpResponseMessage anon = await _client.GetAsync("/api/v1/friends");
        anon.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        string guestToken = _factory.Services.GetRequiredService<IJwtTokenGenerator>().GenerateGuestToken().Token;
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", guestToken);
        HttpResponseMessage guest = await _client.GetAsync("/api/v1/friends");
        guest.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task FriendsOverviewAndRequestLifecycle_ReturnExpectedHttpStatuses()
    {
        (string token, User me) = CreateRegisteredToken("Elena Ortiz");
        User friend = User.Create("Mateo Díaz", $"mateo.{Guid.NewGuid():N}@sensus.com", "hash", true, DateTime.UtcNow).Value;
        _factory.UserRepository.GetByIdAsync(friend.Id, Arg.Any<CancellationToken>()).Returns(friend);

        FriendRequest accepted = FriendRequest.Create(
            me.Id, me.NombreCompleto, "SNS-AAAA",
            friend.Id, friend.NombreCompleto, "SNS-BBBB",
            "QR", _factory.FakeClock.UtcNow.AddDays(-1)).Value;
        accepted.Accept(_factory.FakeClock.UtcNow.AddDays(-1));

        _factory.FriendRequestRepository.GetForUserAsync(me.Id, Arg.Any<CancellationToken>())
            .Returns([accepted]);

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        HttpResponseMessage overviewResp = await _client.GetAsync("/api/v1/friends");
        overviewResp.StatusCode.Should().Be(HttpStatusCode.OK);
        FriendsOverviewDto? overview = await overviewResp.Content.ReadFromJsonAsync<FriendsOverviewDto>();
        overview.Should().NotBeNull();
        overview!.Friends.Should().ContainSingle(f => f.UserId == friend.Id && f.DisplayName == "Mateo Díaz");

        FriendRequest missingPending = FriendRequest.Create(
            friend.Id, friend.NombreCompleto, "SNS-BBBB",
            me.Id, me.NombreCompleto, "SNS-AAAA",
            "QR", _factory.FakeClock.UtcNow).Value;
        _factory.FriendRequestRepository.GetByIdAsync(missingPending.Id, Arg.Any<CancellationToken>())
            .Returns(missingPending);

        HttpResponseMessage acceptResp = await _client.PostAsync($"/api/v1/friends/requests/{missingPending.Id}/accept", null);
        acceptResp.StatusCode.Should().Be(HttpStatusCode.OK);
        await _factory.FriendRequestRepository.Received(1).UpdateAsync(
            Arg.Is<FriendRequest>(r => r.Id == missingPending.Id && r.Status == FriendRequestStatus.Accepted),
            Arg.Any<CancellationToken>());

        _factory.FriendRequestRepository.GetByIdAsync(Arg.Is<Guid>(id => id != missingPending.Id), Arg.Any<CancellationToken>())
            .Returns((FriendRequest?)null);
        HttpResponseMessage notFoundResp = await _client.PostAsync($"/api/v1/friends/requests/{Guid.NewGuid()}/reject", null);
        notFoundResp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task RemoveFriend_ReturnsNoContent_AndGetSharedOffers_ReturnsOk()
    {
        (string token, User me) = CreateRegisteredToken("Valeria Ríos");
        User friend = User.Create("Diego Cruz", $"diego.{Guid.NewGuid():N}@sensus.com", "hash", true, DateTime.UtcNow).Value;
        FriendRequest link = FriendRequest.Create(
            me.Id, me.NombreCompleto, "SNS-1111",
            friend.Id, friend.NombreCompleto, "SNS-2222",
            "Codigo", _factory.FakeClock.UtcNow).Value;
        link.Accept(_factory.FakeClock.UtcNow);

        _factory.FriendRequestRepository.GetForUserAsync(me.Id, Arg.Any<CancellationToken>()).Returns([link]);
        _factory.SharedOfferRepository.GetConversationAsync(me.Id, friend.Id, Arg.Any<CancellationToken>()).Returns([]);

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        HttpResponseMessage sharedResp = await _client.GetAsync($"/api/v1/friends/{friend.Id}/shared-offers");
        sharedResp.StatusCode.Should().Be(HttpStatusCode.OK);

        HttpResponseMessage removeResp = await _client.DeleteAsync($"/api/v1/friends/{friend.Id}");
        removeResp.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }
}
