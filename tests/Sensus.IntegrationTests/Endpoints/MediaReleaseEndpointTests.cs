using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Media.Commands.ReleaseQuarantinedMedia;
using Sensus.Domain.Entities;
using Sensus.Domain.Enums;
using Sensus.IntegrationTests.Infrastructure;
using Xunit;

namespace Sensus.IntegrationTests.Endpoints;

/// <summary>
/// Pruebas de integración de <c>POST /api/v1/media/{id}/release</c> (Spec 019, FR-019-04).
/// </summary>
public class MediaReleaseEndpointTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public MediaReleaseEndpointTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Release_AsAdministrator_ShouldReturnOkAndApprovedAsset()
    {
        MediaAsset asset = CreateAsset(MediaAssetStatus.Quarantined);
        _factory.MediaAssetRepository.GetByIdAsync(asset.Id, Arg.Any<CancellationToken>()).Returns(asset);

        var response = await SendAsync(asset.Id, CreateToken(RoleType.Administrador));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<ReleasedMediaDto>();
        dto.Should().NotBeNull();
        dto!.MediaId.Should().Be(asset.Id);
        dto.Status.Should().Be("Approved");
        dto.SecureUrl.Should().Be("https://res.cloudinary.com/demo/image/upload/a.jpg");
    }

    [Fact]
    public async Task Release_WhenAssetIsNotQuarantined_ShouldReturnConflictProblem()
    {
        MediaAsset asset = CreateAsset(MediaAssetStatus.Rejected);
        _factory.MediaAssetRepository.GetByIdAsync(asset.Id, Arg.Any<CancellationToken>()).Returns(asset);

        var response = await SendAsync(asset.Id, CreateToken(RoleType.Administrador));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
    }

    [Fact]
    public async Task Release_WhenAssetDoesNotExist_ShouldReturnNotFound()
    {
        Guid missingId = Guid.NewGuid();
        _factory.MediaAssetRepository.GetByIdAsync(missingId, Arg.Any<CancellationToken>()).Returns((MediaAsset?)null);

        var response = await SendAsync(missingId, CreateToken(RoleType.Administrador));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Release_WithInvalidId_ShouldReturnNotFoundRoute()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/media/no-es-un-guid/release");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", CreateToken(RoleType.Administrador));

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Release_WithoutToken_ShouldReturnUnauthorized()
    {
        var response = await SendAsync(Guid.NewGuid(), token: null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData(RoleType.Consumidor)]
    [InlineData(RoleType.Comerciante)]
    public async Task Release_WithoutAdministratorRole_ShouldReturnForbidden(string role)
    {
        var response = await SendAsync(Guid.NewGuid(), CreateToken(role));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Release_WithGuestToken_ShouldReturnForbidden()
    {
        var tokenGenerator = _factory.Services.GetRequiredService<IJwtTokenGenerator>();
        var (guestToken, _) = tokenGenerator.GenerateGuestToken();

        var response = await SendAsync(Guid.NewGuid(), guestToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private async Task<HttpResponseMessage> SendAsync(Guid mediaId, string? token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/media/{mediaId}/release");
        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return await _client.SendAsync(request);
    }

    private string CreateToken(string role)
    {
        var user = User.Create(
            "Usuario Moderación",
            $"moderacion-{Guid.NewGuid():N}@sensus.com",
            "hashed-password",
            true,
            DateTime.UtcNow).Value;
        if (role != RoleType.Consumidor)
        {
            user.AddRole(role);
        }

        _factory.UserRepository.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);

        var tokenGenerator = _factory.Services.GetRequiredService<IJwtTokenGenerator>();
        var (token, _) = tokenGenerator.GenerateToken(user);
        return token;
    }

    private static MediaAsset CreateAsset(MediaAssetStatus status) =>
        MediaAsset.Create(
            Guid.NewGuid(),
            "sensus/pending/a",
            "https://res.cloudinary.com/demo/image/upload/a.jpg",
            "image/jpeg",
            status,
            DateTime.UtcNow.AddHours(-2));
}
