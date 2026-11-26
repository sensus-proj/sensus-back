using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Users.Commands.Login;
using Sensus.Application.Users.Commands.RegisterUser;
using Sensus.Application.Users.DTOs;
using Sensus.Domain.Entities;
using Sensus.IntegrationTests.Infrastructure;

namespace Sensus.IntegrationTests.Authentication;

/// <summary>
/// Pruebas de integración para el registro (<c>Spec 001</c>) e inicio de sesión (<c>Spec 002</c>).
/// </summary>
public sealed class AuthEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private static readonly JsonSerializerOptions WebJsonOptions = new(JsonSerializerDefaults.Web);

    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public AuthEndpointsTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Register_WhenValid_Returns201WithJwtAndNoPasswordHash()
    {
        var email = $"nuevo.{Guid.NewGuid():N}@sensus.app";
        _factory.UserRepository.ExistsByEmailAsync(email, Arg.Any<CancellationToken>()).Returns(false);

        var response = await _client.PostAsJsonAsync(
            "/api/v1/auth/register",
            new RegisterUserCommand("Sofía Morales", email, "ClaveSegura123!", true));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var rawJson = await response.Content.ReadAsStringAsync();
        rawJson.Should().NotContainEquivalentOf("passwordHash");
        var body = JsonSerializer.Deserialize<AuthResponseDto>(rawJson, WebJsonOptions);
        body.Should().NotBeNull();
        body!.Token.Should().NotBeNullOrWhiteSpace();
        body.User.Correo.Should().Be(email);
    }

    [Fact]
    public async Task Register_WhenEmailExists_Returns409Conflict()
    {
        var email = $"duplicado.{Guid.NewGuid():N}@sensus.app";
        _factory.UserRepository.ExistsByEmailAsync(email, Arg.Any<CancellationToken>()).Returns(true);

        var response = await _client.PostAsJsonAsync(
            "/api/v1/auth/register",
            new RegisterUserCommand("Sofía Morales", email, "ClaveSegura123!", true));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Register_WhenInputInvalid_Returns400BadRequest()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/v1/auth/register",
            new RegisterUserCommand("A", "correo-invalido", "123", false));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Login_WhenValid_Returns200WithStandardClaims_AndUnknownEmailMatchesWrongPasswordError()
    {
        var hasher = _factory.Services.GetRequiredService<IPasswordHasher>();
        var user = User.Create(
            "Carlos Login",
            $"carlos.{Guid.NewGuid():N}@sensus.app",
            hasher.HashPassword("ClaveCorrecta123!"),
            true,
            DateTime.UtcNow).Value;

        var missingEmail = $"ausente.{Guid.NewGuid():N}@sensus.app";
        _factory.UserRepository.GetByEmailAsync(user.Correo, Arg.Any<CancellationToken>()).Returns(user);
        _factory.UserRepository.GetByEmailAsync(missingEmail, Arg.Any<CancellationToken>()).Returns((User?)null);

        var okResp = await _client.PostAsJsonAsync("/api/v1/auth/login", new LoginCommand(user.Correo, "ClaveCorrecta123!"));
        okResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var auth = await okResp.Content.ReadFromJsonAsync<AuthResponseDto>();
        auth.Should().NotBeNull();

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(auth!.Token);
        jwt.Claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.Sub && c.Value == user.Id.ToString());
        jwt.Claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.Email && c.Value == user.Correo);
        jwt.Claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.Jti);

        var wrongPassResp = await _client.PostAsJsonAsync("/api/v1/auth/login", new LoginCommand(user.Correo, "ClaveIncorrecta999!"));
        var missingUserResp = await _client.PostAsJsonAsync("/api/v1/auth/login", new LoginCommand(missingEmail, "ClaveIncorrecta999!"));

        wrongPassResp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        missingUserResp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var wrongBody = await wrongPassResp.Content.ReadAsStringAsync();
        var missingBody = await missingUserResp.Content.ReadAsStringAsync();
        using var wrongDoc = JsonDocument.Parse(wrongBody);
        using var missingDoc = JsonDocument.Parse(missingBody);
        wrongDoc.RootElement.GetProperty("detail").GetString()
            .Should().Be(missingDoc.RootElement.GetProperty("detail").GetString());
    }
}
