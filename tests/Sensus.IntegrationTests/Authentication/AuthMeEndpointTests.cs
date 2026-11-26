using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Sensus.Application.Common.Interfaces;
using Sensus.Domain.Entities;
using Sensus.Domain.Enums;
using Sensus.IntegrationTests.Infrastructure;

namespace Sensus.IntegrationTests.Authentication;

public sealed class AuthMeEndpointTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public AuthMeEndpointTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _factory.FakeClock.Set(DateTime.UtcNow);
    }

    [Fact]
    public async Task GetMe_WithoutToken_ReturnsUnauthorized()
    {
        HttpResponseMessage response = await _client.GetAsync("/api/v1/auth/me");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetMe_WithValidUserToken_ReturnsExpectedExactJsonSchema()
    {
        var now = DateTime.UtcNow;
        var user = User.Create("Ana Pérez", $"ana.{Guid.NewGuid():N}@sensus.app", "hash", true, now).Value;
        user.AddRole(RoleType.Comerciante);
        _factory.UserRepository.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);

        var (token, _) = _factory.Services.GetRequiredService<IJwtTokenGenerator>().GenerateToken(user);

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        HttpResponseMessage response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = json.RootElement;
        root.EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo(["userId", "nombreCompleto", "correo", "roles"]);
        root.GetProperty("userId").GetString().Should().Be(user.Id.ToString());
        root.GetProperty("nombreCompleto").GetString().Should().Be("Ana Pérez");
        root.GetProperty("correo").GetString().Should().Be(user.Correo);
        root.GetProperty("roles").EnumerateArray().Select(r => r.GetString()).Should().BeEquivalentTo(["Consumidor", "Comerciante"]);
    }

    [Fact]
    public async Task GetMe_WithGuestToken_ReturnsNullCorreoAndGuestRole()
    {
        var (token, _) = _factory.Services.GetRequiredService<IJwtTokenGenerator>().GenerateGuestToken();

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        HttpResponseMessage response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = json.RootElement;
        root.GetProperty("nombreCompleto").GetString().Should().Be("Invitado");
        root.GetProperty("correo").ValueKind.Should().Be(JsonValueKind.Null);
        root.GetProperty("roles").EnumerateArray().Select(r => r.GetString()).Should().Equal("Invitado");
    }
}
