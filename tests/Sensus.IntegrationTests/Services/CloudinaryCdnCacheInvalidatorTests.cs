using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Sensus.Application.Common.Interfaces;
using Sensus.Infrastructure.Services;
using Xunit;

namespace Sensus.IntegrationTests.Services;

/// <summary>
/// Pruebas para CloudinaryCdnCacheInvalidator (Spec 023).
/// Ref: FR-023-04 - invalidar caché al retirar contenido; CA-023-03.
/// </summary>
public sealed class CloudinaryCdnCacheInvalidatorTests
{
    private readonly ILogger<CloudinaryCdnCacheInvalidator> _logger = Substitute.For<ILogger<CloudinaryCdnCacheInvalidator>>();

    private sealed class DelegatingMockHandler(Func<HttpRequestMessage, HttpResponseMessage> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(handler(request));
        }
    }

    private static IOptions<CloudinarySettings> CreateSettings(
        string cloudName = "demo-cloud",
        string apiKey = "demo-api-key",
        string apiSecret = "demo-api-secret") =>
        Options.Create(new CloudinarySettings
        {
            CloudName = cloudName,
            ApiKey = apiKey,
            ApiSecret = apiSecret
        });

    [Fact]
    public async Task InvalidateAsync_WhenPublicIdIsEmpty_ReturnsTrueImmediately()
    {
        // Ref: FR-023-04 - si no hay publicId, no hay nada que invalidar
        var handler = new DelegatingMockHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var invalidator = new CloudinaryCdnCacheInvalidator(new HttpClient(handler), CreateSettings(), _logger);

        bool result = await invalidator.InvalidateAsync("", CancellationToken.None);

        result.Should().BeTrue();
    }

    [Theory]
    [InlineData("", "key", "secret")]
    [InlineData("cloud", "", "secret")]
    [InlineData("cloud", "key", "")]
    public async Task InvalidateAsync_WhenSettingsAreIncomplete_ReturnsFalseWithoutCallingApi(
        string cloudName,
        string apiKey,
        string apiSecret)
    {
        var handler = new DelegatingMockHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var invalidator = new CloudinaryCdnCacheInvalidator(
            new HttpClient(handler),
            CreateSettings(cloudName, apiKey, apiSecret),
            _logger);

        bool result = await invalidator.InvalidateAsync("sensus/media/sample", CancellationToken.None);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task InvalidateAsync_WhenCloudinaryResponds200Ok_ReturnsTrue()
    {
        // Ref: FR-023-04 - invalidación exitosa
        var handler = new DelegatingMockHandler(req =>
        {
            req.Method.Should().Be(HttpMethod.Post);
            req.RequestUri!.ToString().Should().Be("https://api.cloudinary.com/v1_1/demo-cloud/invalidate");
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        var invalidator = new CloudinaryCdnCacheInvalidator(new HttpClient(handler), CreateSettings(), _logger);

        bool result = await invalidator.InvalidateAsync("sensus/media/sample-123", CancellationToken.None);

        result.Should().BeTrue();
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task InvalidateAsync_WhenCloudinaryRespondsError_ReturnsFalse(HttpStatusCode statusCode)
    {
        // Ref: FR-023-04 - mejor esfuerzo: un fallo no lanza excepción
        var handler = new DelegatingMockHandler(_ => new HttpResponseMessage(statusCode));
        var invalidator = new CloudinaryCdnCacheInvalidator(new HttpClient(handler), CreateSettings(), _logger);

        bool result = await invalidator.InvalidateAsync("sensus/media/sample-123", CancellationToken.None);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task InvalidateAsync_WhenHttpClientThrowsException_ReturnsFalseGracefully()
    {
        var handler = new DelegatingMockHandler(_ => throw new HttpRequestException("Network failure"));
        var invalidator = new CloudinaryCdnCacheInvalidator(new HttpClient(handler), CreateSettings(), _logger);

        bool result = await invalidator.InvalidateAsync("sensus/media/sample-123", CancellationToken.None);

        result.Should().BeFalse();
    }
}
