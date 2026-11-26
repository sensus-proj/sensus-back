using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Sensus.Application.Common.Interfaces;
using Sensus.Domain.Entities;
using Sensus.IntegrationTests.Infrastructure;
using Xunit;

namespace Sensus.IntegrationTests.Authentication;

public sealed class LogoutDeviceEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public LogoutDeviceEndpointsTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private (User User, string Token) CreateAuthenticatedUser()
    {
        var user = User.Create(
            "Logout Device User",
            $"logout.device.{Guid.NewGuid():N}@sensus.app",
            "hashed-password",
            true,
            DateTime.UtcNow).Value;

        _factory.UserRepository.GetByIdAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns(user);

        var tokenGenerator = _factory.Services.GetRequiredService<IJwtTokenGenerator>();
        var (token, _) = tokenGenerator.GenerateToken(user);
        return (user, token);
    }

    [Fact]
    public async Task Logout_WithoutDeviceId_RevokesSessionWithoutDeactivatingDevices()
    {
        var (_, token) = CreateAuthenticatedUser();
        _factory.DeviceRepository.ClearReceivedCalls();

        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/logout");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var resp = await _client.SendAsync(req);

        resp.StatusCode.Should().Be(HttpStatusCode.NoContent);
        await _factory.DeviceRepository.DidNotReceive()
            .DeactivateByTokenAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>());

        using var meReq = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/me");
        meReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var meResp = await _client.SendAsync(meReq);
        meResp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Logout_WithOwnDeviceId_DeactivatesOnlyTargetDeviceAndKeepsSiblingsActive()
    {
        var (user, token) = CreateAuthenticatedUser();
        var ownDevice = Device.Create(user.Id, "dev-own-1", "android", "fcm-own-1", DateTime.UtcNow);
        var siblingDevice = Device.Create(user.Id, "dev-own-2", "android", "fcm-own-2", DateTime.UtcNow);

        _factory.DeviceRepository.ClearReceivedCalls();
        _factory.DeviceRepository.GetByUserAndDeviceIdAsync(user.Id, "dev-own-1", Arg.Any<CancellationToken>())
            .Returns(ownDevice);
        _factory.DeviceRepository.GetByUserAndDeviceIdAsync(user.Id, "dev-own-2", Arg.Any<CancellationToken>())
            .Returns(siblingDevice);

        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/logout?deviceId=dev-own-1");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var resp = await _client.SendAsync(req);

        resp.StatusCode.Should().Be(HttpStatusCode.NoContent);
        await _factory.DeviceRepository.Received(1)
            .DeactivateByTokenAsync(ownDevice.Id, ownDevice.Token, Arg.Any<CancellationToken>());
        await _factory.DeviceRepository.DidNotReceive()
            .DeactivateByTokenAsync(siblingDevice.Id, siblingDevice.Token, Arg.Any<CancellationToken>());
        siblingDevice.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Logout_WithOtherUsersDeviceId_DoesNotDeactivateAndReturns204()
    {
        var (user, token) = CreateAuthenticatedUser();
        var otherUserId = Guid.NewGuid();
        var otherDevice = Device.Create(otherUserId, "dev-foreign-1", "android", "fcm-foreign-1", DateTime.UtcNow);

        _factory.DeviceRepository.ClearReceivedCalls();
        _factory.DeviceRepository.GetByUserAndDeviceIdAsync(user.Id, "dev-foreign-1", Arg.Any<CancellationToken>())
            .Returns((Device?)null);
        _factory.DeviceRepository.GetByUserAndDeviceIdAsync(otherUserId, "dev-foreign-1", Arg.Any<CancellationToken>())
            .Returns(otherDevice);

        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/logout?deviceId=dev-foreign-1");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var resp = await _client.SendAsync(req);

        resp.StatusCode.Should().Be(HttpStatusCode.NoContent);
        await _factory.DeviceRepository.DidNotReceive()
            .DeactivateByTokenAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        otherDevice.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Logout_WithNonexistentDeviceId_Returns204()
    {
        var (user, token) = CreateAuthenticatedUser();
        _factory.DeviceRepository.ClearReceivedCalls();
        _factory.DeviceRepository.GetByUserAndDeviceIdAsync(user.Id, "dev-missing", Arg.Any<CancellationToken>())
            .Returns((Device?)null);

        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/logout?deviceId=dev-missing");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var resp = await _client.SendAsync(req);

        resp.StatusCode.Should().Be(HttpStatusCode.NoContent);
        await _factory.DeviceRepository.DidNotReceive()
            .DeactivateByTokenAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("excessively-long-device-id-0123456789-0123456789-0123456789-0123456789-0123456789-0123456789-0123456789")]
    public async Task Logout_WithInvalidDeviceId_Returns400AndKeepsSessionActive(string invalidDeviceId)
    {
        var (_, token) = CreateAuthenticatedUser();
        _factory.DeviceRepository.ClearReceivedCalls();

        var url = $"/api/v1/auth/logout?deviceId={Uri.EscapeDataString(invalidDeviceId)}";
        using var req = new HttpRequestMessage(HttpMethod.Post, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var resp = await _client.SendAsync(req);

        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await resp.Content.ReadAsStringAsync();
        body.Should().Contain("Device.InvalidDeviceId");

        // Sesión sigue viva: el mismo token JWT sigue siendo aceptado en /auth/me
        using var meReq = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/me");
        meReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var meResp = await _client.SendAsync(meReq);
        meResp.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
