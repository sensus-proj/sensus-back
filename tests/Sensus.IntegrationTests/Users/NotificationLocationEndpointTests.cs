using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Users.DTOs;
using Sensus.Domain.Entities;
using Sensus.Domain.Repositories;
using Sensus.IntegrationTests.Infrastructure;
using Xunit;

namespace Sensus.IntegrationTests.Users;

public sealed class NotificationLocationEndpointTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>
{
    [Theory]
    [InlineData(null, 19.4, HttpStatusCode.Unauthorized)]
    [InlineData("guest", 19.4, HttpStatusCode.Forbidden)]
    [InlineData("registered", null, HttpStatusCode.BadRequest)]
    [InlineData("registered", 91.0, HttpStatusCode.BadRequest)]
    [InlineData("registered", 19.4, HttpStatusCode.NoContent)]
    public async Task PutLocation_RequiresConsentActionFromRegisteredAccount(
        string? session, double? latitude, HttpStatusCode expected)
    {
        using HttpClient client = factory.CreateClient();
        Guid userId = await SetSessionAsync(client, session);
        const double longitude = -99.1;

        using HttpResponseMessage response = await client.PutAsJsonAsync(
            "/api/v1/users/notification-location", new { latitude, longitude });

        Assert.Equal(expected, response.StatusCode);
        if (expected == HttpStatusCode.NoContent)
        {
            var locations = factory.Services.GetRequiredService<INotificationLocationRepository>();
            await locations.Received(1).SaveAsync(userId, latitude!.Value, longitude,
                Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>());
        }
    }

    [Theory]
    [InlineData(null, HttpStatusCode.Unauthorized)]
    [InlineData("guest", HttpStatusCode.Forbidden)]
    [InlineData("registered", HttpStatusCode.NoContent)]
    public async Task DeleteLocation_DeletesOnlyForAuthenticatedAccount(string? session, HttpStatusCode expected)
    {
        using HttpClient client = factory.CreateClient();
        Guid userId = await SetSessionAsync(client, session);

        using HttpResponseMessage response = await client.DeleteAsync("/api/v1/users/notification-location");

        Assert.Equal(expected, response.StatusCode);
        if (expected == HttpStatusCode.NoContent)
        {
            var locations = factory.Services.GetRequiredService<INotificationLocationRepository>();
            await locations.Received(1).DeleteAsync(userId, Arg.Any<CancellationToken>());
        }
    }

    private async Task<Guid> SetSessionAsync(HttpClient client, string? session)
    {
        if (session is null)
        {
            return Guid.Empty;
        }

        if (session == "guest")
        {
            using HttpResponseMessage response = await client.PostAsync("/api/v1/auth/guest", null);
            response.EnsureSuccessStatusCode();
            var guest = await response.Content.ReadFromJsonAsync<GuestSessionResponseDto>();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", guest!.Token);
            return Guid.Empty;
        }

        User user = User.Create("Test User", "location@example.test", "hashed-pw", true, DateTime.UtcNow).Value;
        factory.UserRepository.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);
        (string token, _) = factory.Services.GetRequiredService<IJwtTokenGenerator>().GenerateToken(user);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return user.Id;
    }
}
