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

public sealed class OpenAiImageModerationServiceTests
{
    private readonly ILogger<OpenAiImageModerationService> _logger = Substitute.For<ILogger<OpenAiImageModerationService>>();

    private sealed class DelegatingMockHandler(Func<HttpRequestMessage, HttpResponseMessage> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(handler(request));
        }
    }

    [Fact]
    public async Task ModerateImageBytesAsync_WhenBytesAreEmpty_ReturnsRejected()
    {
        // Arrange
        var httpClient = new HttpClient(new DelegatingMockHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)));
        var options = Options.Create(new OpenAiSettings { ApiKey = "test-key" });
        var service = new OpenAiImageModerationService(httpClient, options, _logger);

        // Act
        var result = await service.ModerateImageBytesAsync([]);

        // Assert
        result.IsApproved.Should().BeFalse();
        result.Message.Should().Be("La imagen no puede estar vacía.");
    }

    [Fact]
    public async Task ModerateImageBytesAsync_WhenNoApiKeyConfigured_ReturnsApprovedBypass()
    {
        // Arrange
        var prevEnv = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        try
        {
            Environment.SetEnvironmentVariable("OPENAI_API_KEY", null);
            var httpClient = new HttpClient(new DelegatingMockHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)));
            var options = Options.Create(new OpenAiSettings { ApiKey = "" });
            var service = new OpenAiImageModerationService(httpClient, options, _logger);

            // Act
            var result = await service.ModerateImageBytesAsync([1, 2, 3]);

            // Assert
            result.IsApproved.Should().BeTrue();
            result.Message.Should().Contain("Moderación omitida");
        }
        finally
        {
            Environment.SetEnvironmentVariable("OPENAI_API_KEY", prevEnv);
        }
    }

    [Fact]
    public async Task ModerateImageBytesAsync_WhenOpenAiApprovesImage_ReturnsApproved()
    {
        // Arrange
        var jsonResponse = JsonSerializer.Serialize(new
        {
            id = "modr-123",
            model = "omni-moderation-latest",
            results = new[]
            {
                new
                {
                    flagged = false,
                    categories = new Dictionary<string, bool>
                    {
                        ["hate"] = false,
                        ["violence"] = false
                    }
                }
            }
        });

        var handler = new DelegatingMockHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(jsonResponse, Encoding.UTF8, "application/json")
        });

        var httpClient = new HttpClient(handler);
        var options = Options.Create(new OpenAiSettings { ApiKey = "sk-valid-key" });
        var service = new OpenAiImageModerationService(httpClient, options, _logger);

        // Act
        var result = await service.ModerateImageBytesAsync([10, 20, 30], "image/jpeg");

        // Assert
        result.IsApproved.Should().BeTrue();
        result.Message.Should().Be("Imagen aprobada.");
    }

    [Fact]
    public async Task ModerateImageBytesAsync_WhenOpenAiFlagsImage_ReturnsRejectedWithCategories()
    {
        // Arrange
        var jsonResponse = JsonSerializer.Serialize(new
        {
            id = "modr-456",
            model = "omni-moderation-latest",
            results = new[]
            {
                new
                {
                    flagged = true,
                    categories = new Dictionary<string, bool>
                    {
                        ["hate"] = true,
                        ["violence"] = false
                    }
                }
            }
        });

        var handler = new DelegatingMockHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(jsonResponse, Encoding.UTF8, "application/json")
        });

        var httpClient = new HttpClient(handler);
        var options = Options.Create(new OpenAiSettings { ApiKey = "sk-valid-key" });
        var service = new OpenAiImageModerationService(httpClient, options, _logger);

        // Act
        var result = await service.ModerateImageBytesAsync([10, 20, 30], "image/png");

        // Assert
        result.IsApproved.Should().BeFalse();
        result.Message.Should().Be(OpenAiImageModerationService.PolicyViolationMessage);
        result.FlaggedCategories.Should().Contain("hate");
    }

    [Fact]
    public async Task ModerateImageBytesAsync_WhenResultsArrayIsEmpty_ReturnsApproved()
    {
        // Arrange
        var jsonResponse = JsonSerializer.Serialize(new
        {
            id = "modr-789",
            model = "omni-moderation-latest",
            results = Array.Empty<object>()
        });

        var handler = new DelegatingMockHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(jsonResponse, Encoding.UTF8, "application/json")
        });

        var httpClient = new HttpClient(handler);
        var options = Options.Create(new OpenAiSettings { ApiKey = "sk-valid-key" });
        var service = new OpenAiImageModerationService(httpClient, options, _logger);

        // Act
        var result = await service.ModerateImageBytesAsync([1, 2, 3]);

        // Assert
        result.IsApproved.Should().BeTrue();
    }

    [Fact]
    public async Task ModerateImageBytesAsync_WhenHttpReturns500_ReturnsRejected()
    {
        // Arrange
        var handler = new DelegatingMockHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = new StringContent("Server error", Encoding.UTF8, "text/plain")
        });

        var httpClient = new HttpClient(handler);
        var options = Options.Create(new OpenAiSettings { ApiKey = "sk-valid-key" });
        var service = new OpenAiImageModerationService(httpClient, options, _logger);

        // Act
        var result = await service.ModerateImageBytesAsync([1, 2, 3]);

        // Assert
        result.IsApproved.Should().BeFalse();
        result.Message.Should().Be(OpenAiImageModerationService.PolicyViolationMessage);
    }
}
