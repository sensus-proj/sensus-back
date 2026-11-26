using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Sensus.Application.Common.Interfaces;
using Sensus.Domain.Entities;
using Sensus.Domain.Enums;
using Sensus.IntegrationTests.Infrastructure;
using Xunit;

namespace Sensus.IntegrationTests.Security;

/// <summary>
/// Regresión de FR-036-09 y FR-033-12 (OWASP API5): los disparadores de expiración eran públicos en producción.
/// Ahora exigen el rol Administrador y nunca ejecutan el proceso para otros llamantes.
/// </summary>
public sealed class ExpireCheckAuthorizationTests : IClassFixture<CustomWebApplicationFactory>
{
    private const string OffersRoute = "/api/v1/offers/expire-check";
    private const string ClaimsRoute = "/api/v1/offers/claims/expire-check";

    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public ExpireCheckAuthorizationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Theory]
    [InlineData(OffersRoute)]
    [InlineData(ClaimsRoute)]
    public async Task WithoutToken_ReturnsUnauthorized_AndDoesNotRunTheJob(string route)
    {
        _factory.OfferRepository.ClearReceivedCalls();
        _factory.OfferClaimRepository.ClearReceivedCalls();

        var response = await SendAsync(route, token: null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        await _factory.OfferRepository.DidNotReceive().ExpireOffersAsync(Arg.Any<DateTime>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(OffersRoute, RoleType.Consumidor)]
    [InlineData(OffersRoute, RoleType.Comerciante)]
    [InlineData(ClaimsRoute, RoleType.Consumidor)]
    [InlineData(ClaimsRoute, RoleType.Comerciante)]
    public async Task WithoutAdministratorRole_ReturnsForbidden(string route, string role)
    {
        var response = await SendAsync(route, CreateToken(role));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Theory]
    [InlineData(OffersRoute)]
    [InlineData(ClaimsRoute)]
    public async Task WithGuestToken_ReturnsForbidden(string route)
    {
        var (guestToken, _) = _factory.Services.GetRequiredService<IJwtTokenGenerator>().GenerateGuestToken();

        var response = await SendAsync(route, guestToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task OffersExpireCheck_AsAdministrator_RunsTheJob()
    {
        _factory.OfferRepository.ExpireOffersAsync(Arg.Any<DateTime>(), Arg.Any<CancellationToken>()).Returns(3);

        var response = await SendAsync(OffersRoute, CreateToken(RoleType.Administrador));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Contain("\"expiredCount\":3");
    }

    [Fact]
    public async Task ClaimsExpireCheck_AsAdministrator_RunsTheJob()
    {
        _factory.OfferClaimRepository
            .ExpireClaimsAsync(Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<OfferClaim>)[]);

        var response = await SendAsync(ClaimsRoute, CreateToken(RoleType.Administrador));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private async Task<HttpResponseMessage> SendAsync(string route, string? token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, route);
        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return await _client.SendAsync(request);
    }

    private string CreateToken(string role)
    {
        var user = User.Create("Prueba", $"exp-{Guid.NewGuid():N}@sensus.com", "hash", true, DateTime.UtcNow).Value;
        if (role != RoleType.Consumidor)
        {
            user.AddRole(role);
        }

        _factory.UserRepository.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);
        var jwtGen = _factory.Services.GetRequiredService<IJwtTokenGenerator>();
        var clock = _factory.Services.GetRequiredService<IDateTimeProvider>();
        var (token, _) = role == RoleType.Administrador
            ? jwtGen.GenerateAdminStepUpToken(user, clock.UtcNow)
            : jwtGen.GenerateToken(user);
        return token;
    }
}
