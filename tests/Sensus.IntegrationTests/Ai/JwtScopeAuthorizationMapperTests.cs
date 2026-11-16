using System.Security.Claims;
using FluentAssertions;
using Sensus.Domain.Enums;
using Sensus.Infrastructure.Services;

namespace Sensus.IntegrationTests.Ai;

public class JwtScopeAuthorizationMapperTests
{
    private readonly JwtScopeAuthorizationMapper _mapper = new();

    [Fact]
    public void MapScopes_ShouldReturnEmpty_WhenUserIsNotAuthenticated()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity());
        var scopes = _mapper.MapScopes(principal);

        scopes.Should().BeEmpty();
    }

    [Fact]
    public void MapScopes_ShouldReturnEmpty_WhenUserIsGuest()
    {
        var identity = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.Role, RoleType.Invitado)
        }, "Bearer");

        var principal = new ClaimsPrincipal(identity);
        var scopes = _mapper.MapScopes(principal);

        scopes.Should().BeEmpty();
    }

    [Fact]
    public void MapScopes_ShouldReturnConsumerScopes_WhenUserIsConsumer()
    {
        var identity = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.Role, RoleType.Consumidor)
        }, "Bearer");

        var principal = new ClaimsPrincipal(identity);
        var scopes = _mapper.MapScopes(principal);

        scopes.Should().Contain(AiScope.AiChat);
        scopes.Should().Contain(AiScope.OffersRead);
        scopes.Should().Contain(AiScope.AccountRead);
        scopes.Should().Contain(AiScope.ClaimsRead);
        scopes.Should().NotContain(AiScope.MerchantRead);
    }

    [Fact]
    public void MapScopes_ShouldIncludeMerchantRead_WhenUserIsMerchant()
    {
        var identity = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.Role, RoleType.Comerciante)
        }, "Bearer");

        var principal = new ClaimsPrincipal(identity);
        var scopes = _mapper.MapScopes(principal);

        scopes.Should().Contain(AiScope.MerchantRead);
        scopes.Should().Contain(AiScope.AiChat);
    }

    [Theory]
    [InlineData(RoleType.Consumidor, false)]
    [InlineData(RoleType.Comerciante, true)]
    [InlineData(RoleType.Invitado, false)]
    public void MapScopesByRole_ShouldBeConsistentWithPrincipalMapping(string role, bool shouldHaveMerchantRead)
    {
        var scopes = _mapper.MapScopesByRole(role);
        if (role == RoleType.Invitado)
        {
            scopes.Should().BeEmpty();
        }
        else
        {
            scopes.Contains(AiScope.MerchantRead).Should().Be(shouldHaveMerchantRead);
        }
    }
}
