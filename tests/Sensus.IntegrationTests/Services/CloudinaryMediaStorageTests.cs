using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Sensus.Infrastructure.Services;
using Xunit;

namespace Sensus.IntegrationTests.Services;

public sealed class CloudinaryMediaStorageTests
{
    private readonly ILogger<CloudinaryMediaStorage> _logger = Substitute.For<ILogger<CloudinaryMediaStorage>>();

    private sealed class DelegatingMockHandler(Func<HttpRequestMessage, HttpResponseMessage> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(handler(request));
        }
    }

    [Fact]
    public async Task UploadImageAsync_WhenCloudinaryReturnsSnakeCaseJson_DeserializesPublicIdAndSecureUrl()
    {
        // Arrange
        var jsonResponse = JsonSerializer.Serialize(new
        {
            public_id = "sensus/media/owner/sample-123",
            secure_url = "https://res.cloudinary.com/demo/image/upload/sample-123.jpg"
        });

        var handler = new DelegatingMockHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(jsonResponse, Encoding.UTF8, "application/json")
        });

        var options = Options.Create(new CloudinarySettings
        {
            CloudName = "demo-cloud",
            ApiKey = "demo-key",
            ApiSecret = "demo-secret"
        });

        var storage = new CloudinaryMediaStorage(new HttpClient(handler), options, _logger);

        // Act
        var result = await storage.UploadImageAsync([1, 2, 3], "image/jpeg", Guid.NewGuid());

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.PublicId.Should().Be("sensus/media/owner/sample-123");
        result.Value.SecureUrl.Should().Be("https://res.cloudinary.com/demo/image/upload/sample-123.jpg");
    }

    [Fact]
    public async Task UploadImageAsync_WhenCloudinaryOmitsPublicId_ReturnsFailure()
    {
        // Arrange
        var handler = new DelegatingMockHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"secure_url\":\"https://example.com/a.jpg\"}", Encoding.UTF8, "application/json")
        });

        var options = Options.Create(new CloudinarySettings
        {
            CloudName = "demo-cloud",
            ApiKey = "demo-key",
            ApiSecret = "demo-secret"
        });

        var storage = new CloudinaryMediaStorage(new HttpClient(handler), options, _logger);

        // Act
        var result = await storage.UploadImageAsync([1, 2, 3], "image/png", Guid.NewGuid());

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Media.InvalidResponse");
    }

    [Fact]
    public async Task UploadImageAsync_SendsAuthenticatedTypeAndSignsTypeParameter()
    {
        Dictionary<string, string> capturedFields = new(StringComparer.Ordinal);
        Guid ownerId = Guid.NewGuid();
        var jsonResponse = JsonSerializer.Serialize(new
        {
            public_id = $"sensus/media/{ownerId}/sample",
            secure_url = $"https://res.cloudinary.com/demo-cloud/image/authenticated/v1/sensus/media/{ownerId}/sample.jpg"
        });

        var handler = new DelegatingMockHandler(request =>
        {
            if (request.Content is MultipartFormDataContent multipart)
            {
                foreach (HttpContent part in multipart)
                {
                    string? name = part.Headers.ContentDisposition?.Name?.Trim('"');
                    if (!string.IsNullOrWhiteSpace(name) && name != "file")
                    {
                        capturedFields[name] = part.ReadAsStringAsync().GetAwaiter().GetResult();
                    }
                }
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(jsonResponse, Encoding.UTF8, "application/json")
            };
        });

        var options = Options.Create(new CloudinarySettings
        {
            CloudName = "demo-cloud",
            ApiKey = "demo-key",
            ApiSecret = "demo-secret"
        });
        var storage = new CloudinaryMediaStorage(new HttpClient(handler), options, _logger);

        var result = await storage.UploadImageAsync([0xFF, 0xD8, 0xFF], "image/jpeg", ownerId);

        result.IsSuccess.Should().BeTrue();
        capturedFields.Should().ContainKey("type").WhoseValue.Should().Be("authenticated");
        string expectedToSign =
            $"folder=sensus/media/{ownerId}&timestamp={capturedFields["timestamp"]}&type=authenticateddemo-secret";
        string expectedSig = Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(expectedToSign))).ToLowerInvariant();
        capturedFields["signature"].Should().Be(expectedSig);
    }
}
