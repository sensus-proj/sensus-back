using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Users.DTOs;
using Sensus.Domain.Entities;
using Sensus.IntegrationTests.Infrastructure;

namespace Sensus.IntegrationTests.Authentication;

public sealed class RefreshTokenEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public RefreshTokenEndpointsTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Login_Issues15MinJwtAndRotatingRefreshToken_AndReuseRevokesFamily()
    {
        var passwordHasher = _factory.Services.GetRequiredService<IPasswordHasher>();
        DateTime now = DateTime.UtcNow;
        _factory.FakeClock.Set(now);
        User user = User.Create("Usuario Sesión", $"session.{Guid.NewGuid():N}@sensus.app", passwordHasher.HashPassword("ClaveSegura123!"), true, now).Value;

        _factory.UserRepository.GetByEmailAsync(user.Correo, Arg.Any<CancellationToken>()).Returns(user);
        _factory.UserRepository.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);

        HttpResponseMessage loginResponse = await _client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new { correo = user.Correo, password = "ClaveSegura123!" });

        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        AuthResponseDto? loginAuth = await loginResponse.Content.ReadFromJsonAsync<AuthResponseDto>();
        loginAuth.Should().NotBeNull();
        loginAuth!.ExpiresAtUtc.Should().BeCloseTo(now.AddMinutes(15), TimeSpan.FromSeconds(5));
        loginAuth.RefreshToken.Should().NotBeNullOrWhiteSpace();
        loginAuth.RefreshExpiresAtUtc.Should().NotBeNull();

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(loginAuth.Token);
        (jwt.ValidTo - jwt.ValidFrom).Should().BeLessThanOrEqualTo(TimeSpan.FromMinutes(15));

        // 1. Primera rotación: exitosa y devuelve nuevo par de tokens
        HttpResponseMessage refresh1 = await _client.PostAsJsonAsync(
            "/api/v1/auth/refresh",
            new { refreshToken = loginAuth.RefreshToken });

        refresh1.StatusCode.Should().Be(HttpStatusCode.OK);
        AuthResponseDto? rotatedAuth = await refresh1.Content.ReadFromJsonAsync<AuthResponseDto>();
        rotatedAuth.Should().NotBeNull();
        rotatedAuth!.RefreshToken.Should().NotBeNullOrWhiteSpace();
        rotatedAuth.RefreshToken.Should().NotBe(loginAuth.RefreshToken);

        // 2. Reutilización del token ya rotado: devuelve 401 y revoca toda la familia + SecurityStamp
        HttpResponseMessage replay = await _client.PostAsJsonAsync(
            "/api/v1/auth/refresh",
            new { refreshToken = loginAuth.RefreshToken });
        replay.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // El token nuevo de la misma familia también queda revocado
        HttpResponseMessage afterCompromise = await _client.PostAsJsonAsync(
            "/api/v1/auth/refresh",
            new { refreshToken = rotatedAuth.RefreshToken });
        afterCompromise.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // El access token previo también queda inválido porque rotó SecurityStamp
        using var meReq = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/me");
        meReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", rotatedAuth.Token);
        HttpResponseMessage meResp = await _client.SendAsync(meReq);
        meResp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Logout_RevokesActiveRefreshTokens()
    {
        var passwordHasher = _factory.Services.GetRequiredService<IPasswordHasher>();
        DateTime now = DateTime.UtcNow;
        _factory.FakeClock.Set(now);
        User user = User.Create("Usuario Logout", $"logout.{Guid.NewGuid():N}@sensus.app", passwordHasher.HashPassword("ClaveSegura123!"), true, now).Value;

        _factory.UserRepository.GetByEmailAsync(user.Correo, Arg.Any<CancellationToken>()).Returns(user);
        _factory.UserRepository.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);

        HttpResponseMessage loginResponse = await _client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new { correo = user.Correo, password = "ClaveSegura123!" });
        AuthResponseDto? loginAuth = await loginResponse.Content.ReadFromJsonAsync<AuthResponseDto>();

        using var logoutReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/logout");
        logoutReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", loginAuth!.Token);
        HttpResponseMessage logoutResp = await _client.SendAsync(logoutReq);
        logoutResp.StatusCode.Should().Be(HttpStatusCode.NoContent);

        HttpResponseMessage refreshAfterLogout = await _client.PostAsJsonAsync(
            "/api/v1/auth/refresh",
            new { refreshToken = loginAuth.RefreshToken });
        refreshAfterLogout.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
