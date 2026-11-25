using System.Diagnostics;
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

/// <summary>
/// Spec 044: contrato HTTP de preferencias de notificación, valores por defecto, validación de radio
/// y latencia de lectura (NFR-044-01).
/// </summary>
public sealed class NotificationSettingsEndpointTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly INotificationSettingsRepository _settings;

    public NotificationSettingsEndpointTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _settings = _factory.Services.GetRequiredService<INotificationSettingsRepository>();
        _settings.ClearReceivedCalls();
    }

    [Fact]
    public async Task GetSettings_RequiresRegisteredAccount_AndReturnsDefaultsWhenNoneExist()
    {
        using HttpClient anonymous = _factory.CreateClient();
        using HttpResponseMessage anonymousResponse = await anonymous.GetAsync("/api/v1/users/settings");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousResponse.StatusCode);

        using HttpClient guest = _factory.CreateClient();
        await SetSessionAsync(guest, "guest");
        using HttpResponseMessage guestResponse = await guest.GetAsync("/api/v1/users/settings");
        Assert.Equal(HttpStatusCode.Forbidden, guestResponse.StatusCode);

        using HttpClient client = _factory.CreateClient();
        Guid userId = await SetSessionAsync(client, "registered");
        _settings.GetByUserIdAsync(userId, Arg.Any<CancellationToken>()).Returns((NotificationSettings?)null);

        NotificationSettingsDto? dto = await client.GetFromJsonAsync<NotificationSettingsDto>("/api/v1/users/settings");

        Assert.NotNull(dto);
        Assert.Equal(userId, dto!.UserId);
        Assert.True(dto.PushEnabled);
        Assert.True(dto.OfferAlerts);
        Assert.True(dto.SocialAlerts);
        Assert.InRange(dto.RadiusKm, 1, 50);
        await _settings.Received(1).AddAsync(Arg.Is<NotificationSettings>(s => s.UserId == userId), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(51)]
    public async Task PutSettings_WithRadiusOutOfRange_Returns400(int radiusKm)
    {
        using HttpClient client = _factory.CreateClient();
        Guid userId = await SetSessionAsync(client, "registered");
        _settings.GetByUserIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(NotificationSettings.CreateDefaults(userId));

        using HttpResponseMessage response = await client.PutAsJsonAsync(
            "/api/v1/users/settings", new { pushEnabled = true, offerAlerts = true, socialAlerts = true, radiusKm });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await _settings.DidNotReceive().UpdateAsync(Arg.Any<NotificationSettings>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PutSettings_UpdatesAndReturnsResultingState()
    {
        using HttpClient client = _factory.CreateClient();
        Guid userId = await SetSessionAsync(client, "registered");
        _settings.GetByUserIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(NotificationSettings.CreateDefaults(userId));

        using HttpResponseMessage response = await client.PutAsJsonAsync(
            "/api/v1/users/settings",
            new { pushEnabled = false, offerAlerts = false, socialAlerts = true, radiusKm = 25 });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<NotificationSettingsDto>();
        Assert.NotNull(dto);
        Assert.False(dto!.PushEnabled);
        Assert.False(dto.OfferAlerts);
        Assert.True(dto.SocialAlerts);
        Assert.Equal(25, dto.RadiusKm);
        await _settings.Received(1).UpdateAsync(
            Arg.Is<NotificationSettings>(s => !s.PushEnabled && !s.OfferAlerts && s.SocialAlerts && s.RadiusKm == 25),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetSettings_MeetsLatencyTarget_ForTwentyRequests()
    {
        using HttpClient client = _factory.CreateClient();
        Guid userId = await SetSessionAsync(client, "registered");
        NotificationSettings existing = NotificationSettings.CreateDefaults(userId);
        _settings.GetByUserIdAsync(userId, Arg.Any<CancellationToken>()).Returns(existing);

        List<double> latencies = [];
        for (int i = 0; i < 20; i++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/users/settings");
            request.Headers.Authorization = client.DefaultRequestHeaders.Authorization;
            var stopwatch = Stopwatch.StartNew();
            using HttpResponseMessage response = await client.SendAsync(request);
            stopwatch.Stop();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            latencies.Add(stopwatch.Elapsed.TotalMilliseconds);
        }

        latencies.Sort();
        int p95Index = (int)Math.Ceiling(latencies.Count * 0.95) - 1;
        Assert.True(latencies[p95Index] < 250, $"p94 fue {latencies[p95Index]:F2} ms");
    }

    private async Task<Guid> SetSessionAsync(HttpClient client, string session)
    {
        if (session == "guest")
        {
            using HttpResponseMessage response = await client.PostAsync("/api/v1/auth/guest", null);
            response.EnsureSuccessStatusCode();
            var guest = await response.Content.ReadFromJsonAsync<GuestSessionResponseDto>();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", guest!.Token);
            return Guid.Empty;
        }

        User user = User.Create("Settings User", $"settings.{Guid.NewGuid():N}@example.test", "hashed-pw", true, DateTime.UtcNow).Value;
        _factory.UserRepository.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);
        (string token, _) = _factory.Services.GetRequiredService<IJwtTokenGenerator>().GenerateToken(user);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return user.Id;
    }
}
