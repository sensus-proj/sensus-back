using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Media.Delivery;
using Sensus.Domain.Common;
using Sensus.Domain.Entities;
using Sensus.Domain.Enums;
using Sensus.IntegrationTests.Infrastructure;
using Xunit;

namespace Sensus.IntegrationTests.Endpoints;

/// <summary>
/// Pruebas de integración para los endpoints de entrega multimedia (Spec 023).
/// Ref: FR-023-01 - entrega Cloudinary CDN; FR-023-02 - URLs firmadas; FR-023-03 - cabeceras de caché; CA-023-01 a CA-023-03.
/// </summary>
public sealed class MediaDeliveryEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private static readonly DateTime Now = new(2026, 11, 22, 10, 0, 0, DateTimeKind.Utc);

    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private readonly IMediaDeliveryUrlGenerator _generator;

    public MediaDeliveryEndpointsTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        _generator = factory.Services.GetRequiredService<IMediaDeliveryUrlGenerator>();
    }

    private (string Token, User User) CreateUser(string role = RoleType.Consumidor)
    {
        var user = User.Create(
            "Usuario Media",
            $"media-{Guid.NewGuid():N}@sensus.com",
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
        return (token, user);
    }

    private static MediaAsset CreateAsset(
        Guid ownerId,
        MediaAssetStatus status,
        string secureUrl = "https://res.cloudinary.com/sensus/image/upload/sample.jpg") =>
        MediaAsset.Create(
            ownerId,
            "sensus/media/sample",
            secureUrl,
            "image/jpeg",
            status,
            Now);

    [Fact]
    public async Task CreateDeliveryUrl_WithoutAuthentication_ReturnsUnauthorized()
    {
        // Ref: FR-023-02 - emisión exige autenticación
        Guid mediaId = Guid.NewGuid();
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/media/{mediaId}/delivery-url");

        HttpResponseMessage response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateDeliveryUrl_WhenAssetIsApproved_ReturnsOkWithPublicSignedUrl()
    {
        // Ref: FR-023-01, FR-023-02, CA-023-01
        var (token, user) = CreateUser();
        MediaAsset asset = CreateAsset(user.Id, MediaAssetStatus.Approved);
        _factory.MediaAssetRepository.GetByIdAsync(asset.Id, Arg.Any<CancellationToken>()).Returns(asset);

        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/media/{asset.Id}/delivery-url");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        HttpResponseMessage response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<MediaDeliveryUrlDto>();

        dto.Should().NotBeNull();
        dto!.MediaId.Should().Be(asset.Id);
        dto.Scope.Should().Be("Public");
        dto.Status.Should().Be("Approved");
        dto.CacheMaxAgeSeconds.Should().Be(86400);
        dto.DeliveryUrl.Should().Contain($"/api/v1/media/{asset.Id}/delivery?expires=");
        dto.DeliveryUrl.Should().Contain("&signature=");
    }

    [Theory]
    [InlineData(MediaAssetStatus.Quarantined)]
    [InlineData(MediaAssetStatus.Pending)]
    [InlineData(MediaAssetStatus.Rejected)]
    public async Task CreateDeliveryUrl_WhenAssetIsNotApproved_ReturnsNotFound_FailClosed(MediaAssetStatus status)
    {
        // Ref: B1 / Decision 2 - Solo Approved se entrega por /delivery; cualquier otro estado responde NotFound uniforme.
        var (token, user) = CreateUser();
        MediaAsset asset = CreateAsset(user.Id, status);
        _factory.MediaAssetRepository.GetByIdAsync(asset.Id, Arg.Any<CancellationToken>()).Returns(asset);

        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/media/{asset.Id}/delivery-url");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        HttpResponseMessage response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetDelivery_WithoutSignatureOrExpires_ReturnsForbidden()
    {
        // Ref: CA-023-02 - 403 sin firma
        Guid mediaId = Guid.NewGuid();
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/media/{mediaId}/delivery");

        HttpResponseMessage response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetDelivery_WhenSignatureIsExpired_ReturnsForbidden()
    {
        // Ref: FR-023-02 - URL con vigencia vencida rechazada
        Guid mediaId = Guid.NewGuid();
        long expiredTimestamp = DateTimeOffset.UtcNow.AddMinutes(-5).ToUnixTimeSeconds();
        string url = $"/api/v1/media/{mediaId}/delivery?expires={expiredTimestamp}&signature=some_sig";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        HttpResponseMessage response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetDelivery_WhenSignatureIsInvalid_ReturnsForbidden()
    {
        // Ref: CA-023-02 - firma alterada rechazada con 403
        var (_, user) = CreateUser();
        MediaAsset asset = CreateAsset(user.Id, MediaAssetStatus.Approved);
        _factory.MediaAssetRepository.GetByIdAsync(asset.Id, Arg.Any<CancellationToken>()).Returns(asset);

        long futureTimestamp = DateTimeOffset.UtcNow.AddMinutes(30).ToUnixTimeSeconds();
        string url = $"/api/v1/media/{asset.Id}/delivery?expires={futureTimestamp}&signature=tampered_signature";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        HttpResponseMessage response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetDelivery_WhenApprovedAssetWithValidSignature_ProxiesContentWithPublicCacheHeader()
    {
        // Ref: B2 / FR-023-01 / FR-023-03 - proxy de contenido con Cache-Control acotado al TTL remanente
        var (_, user) = CreateUser();
        MediaAsset asset = CreateAsset(user.Id, MediaAssetStatus.Approved, "https://res.cloudinary.com/sensus/image/upload/sample.jpg");
        _factory.MediaAssetRepository.GetByIdAsync(asset.Id, Arg.Any<CancellationToken>()).Returns(asset);

        byte[] payloadBytes = [1, 2, 3, 4, 5];
        _factory.MediaContentProxy.OpenReadAsync(asset, Arg.Any<CancellationToken>())
            .Returns(Result.Success(new MediaContentPayload(new MemoryStream(payloadBytes), "image/jpeg")));

        Result<MediaDeliveryUrl> deliveryUrlResult = _generator.Generate(asset, TimeSpan.FromMinutes(60));
        deliveryUrlResult.IsSuccess.Should().BeTrue();

        var uri = new Uri(deliveryUrlResult.Value.Url);
        string relativePath = $"{uri.AbsolutePath}{uri.Query}";

        using var request = new HttpRequestMessage(HttpMethod.Get, relativePath);
        HttpResponseMessage response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("image/jpeg");
        response.Headers.CacheControl.Should().NotBeNull();
        response.Headers.CacheControl!.Public.Should().BeTrue();

        byte[] body = await response.Content.ReadAsByteArrayAsync();
        body.Should().Equal(payloadBytes);
    }

    [Fact]
    public async Task GetDelivery_WhenQuarantinedAssetWithValidSignature_ReturnsNotFound_FailClosed()
    {
        // Ref: B1 / Decision 2 - Contenido en cuarentena no se entrega por /delivery (error 404 uniforme)
        var (_, user) = CreateUser();
        MediaAsset asset = CreateAsset(user.Id, MediaAssetStatus.Quarantined);
        _factory.MediaAssetRepository.GetByIdAsync(asset.Id, Arg.Any<CancellationToken>()).Returns(asset);

        Result<MediaDeliveryUrl> deliveryUrlResult = _generator.Generate(asset, TimeSpan.FromMinutes(5));
        deliveryUrlResult.IsSuccess.Should().BeTrue();

        var uri = new Uri(deliveryUrlResult.Value.Url);
        string relativePath = $"{uri.AbsolutePath}{uri.Query}";

        using var request = new HttpRequestMessage(HttpMethod.Get, relativePath);
        HttpResponseMessage response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
