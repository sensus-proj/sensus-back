// Ref: FR-023-01 / FR-023-02 / M2 - Proxy para contenido seguro previniendo SSRF y fugas de origen
using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Common.Settings;
using Sensus.Domain.Common;
using Sensus.Domain.Entities;
using Sensus.Infrastructure.Services;
using Xunit;

namespace Sensus.IntegrationTests.Services;

public sealed class CloudinaryMediaContentProxyTests
{
    private readonly ILogger<CloudinaryMediaContentProxy> _logger = Substitute.For<ILogger<CloudinaryMediaContentProxy>>();
    private readonly MediaDeliverySettings _settings = new()
    {
        ProxyTimeoutSeconds = 5,
        MaxResponseSizeBytes = 1024 * 1024 // 1 MB
    };

    private sealed class DelegatingMockHandler(Func<HttpRequestMessage, HttpResponseMessage> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(handler(request));
        }
    }

    private static MediaAsset CreateAsset(string secureUrl, string contentType = "image/jpeg") =>
        MediaAsset.Create(Guid.NewGuid(), "pid-1", secureUrl, contentType, MediaAssetStatus.Approved, DateTime.UtcNow);

    [Fact]
    public async Task OpenReadAsync_WhenValidCloudinaryHttpsUrl_ReturnsPayloadWithStream()
    {
        byte[] expectedBytes = [10, 20, 30, 40];
        var handler = new DelegatingMockHandler(req =>
        {
            req.RequestUri!.Host.Should().Be("res.cloudinary.com");
            req.RequestUri.Scheme.Should().Be("https");
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(expectedBytes)
            };
        });

        var proxy = new CloudinaryMediaContentProxy(new HttpClient(handler), Options.Create(_settings), _logger);
        MediaAsset asset = CreateAsset("https://res.cloudinary.com/sensus/image/upload/sample.jpg", "image/jpeg");

        Result<MediaContentPayload> result = await proxy.OpenReadAsync(asset, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.ContentType.Should().Be("image/jpeg");

        using var memoryStream = new MemoryStream();
        await result.Value.Content.CopyToAsync(memoryStream);
        memoryStream.ToArray().Should().Equal(expectedBytes);
    }

    [Theory]
    [InlineData("http://res.cloudinary.com/sensus/image/upload/sample.jpg")] // Inseguro HTTP
    [InlineData("https://evil.com/sensus/sample.jpg")] // Dominio no autorizado
    [InlineData("https://localhost:5001/admin/secret")] // Intento SSRF interno
    [InlineData("https://169.254.169.254/latest/meta-data")] // Intento SSRF metadata AWS
    [InlineData("ftp://res.cloudinary.com/sample.jpg")] // Esquema no permitido
    [InlineData("not-a-valid-url")] // URL malformada
    [InlineData(null)] // Nulo
    public async Task OpenReadAsync_WhenUrlIsNotHttpsCloudinaryHost_ReturnsUnavailable_PreventingSsrf(string? maliciousUrl)
    {
        var handler = new DelegatingMockHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var proxy = new CloudinaryMediaContentProxy(new HttpClient(handler), Options.Create(_settings), _logger);
        MediaAsset asset = CreateAsset(maliciousUrl ?? "");

        Result<MediaContentPayload> result = await proxy.OpenReadAsync(asset, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("MediaDelivery.ContentUnavailable");
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadGateway)]
    public async Task OpenReadAsync_WhenUpstreamReturnsErrorStatusCode_ReturnsUnavailable(HttpStatusCode statusCode)
    {
        var handler = new DelegatingMockHandler(_ => new HttpResponseMessage(statusCode));
        var proxy = new CloudinaryMediaContentProxy(new HttpClient(handler), Options.Create(_settings), _logger);
        MediaAsset asset = CreateAsset("https://res.cloudinary.com/sensus/image/upload/sample.jpg");

        Result<MediaContentPayload> result = await proxy.OpenReadAsync(asset, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("MediaDelivery.ContentUnavailable");
    }

    [Fact]
    public async Task OpenReadAsync_WhenContentLengthExceedsLimit_ReturnsPayloadTooLarge()
    {
        var handler = new DelegatingMockHandler(_ =>
        {
            var msg = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(new byte[10])
            };
            msg.Content.Headers.ContentLength = 2 * 1024 * 1024; // 2 MB > 1 MB limit
            return msg;
        });

        var proxy = new CloudinaryMediaContentProxy(new HttpClient(handler), Options.Create(_settings), _logger);
        MediaAsset asset = CreateAsset("https://res.cloudinary.com/sensus/image/upload/sample.jpg");

        Result<MediaContentPayload> result = await proxy.OpenReadAsync(asset, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("MediaDelivery.PayloadTooLarge");
    }

    [Fact]
    public async Task OpenReadAsync_WhenHttpClientThrowsException_ReturnsUnavailable()
    {
        var handler = new DelegatingMockHandler(_ => throw new HttpRequestException("Network failure"));
        var proxy = new CloudinaryMediaContentProxy(new HttpClient(handler), Options.Create(_settings), _logger);
        MediaAsset asset = CreateAsset("https://res.cloudinary.com/sensus/image/upload/sample.jpg");

        Result<MediaContentPayload> result = await proxy.OpenReadAsync(asset, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("MediaDelivery.ContentUnavailable");
    }

    [Fact]
    public async Task OpenReadAsync_DisposingStream_DisposesHttpResponse()
    {
        var handler = new DelegatingMockHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent([1, 2, 3])
        });

        var proxy = new CloudinaryMediaContentProxy(new HttpClient(handler), Options.Create(_settings), _logger);
        MediaAsset asset = CreateAsset("https://res.cloudinary.com/sensus/image/upload/sample.jpg");

        Result<MediaContentPayload> result = await proxy.OpenReadAsync(asset, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await result.Value.Content.DisposeAsync();
        result.Value.Content.CanRead.Should().BeFalse();
    }
}
