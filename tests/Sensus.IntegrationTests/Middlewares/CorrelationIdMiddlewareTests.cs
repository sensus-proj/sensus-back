using System.Net;
using FluentAssertions;
using Sensus.Api.Middlewares;
using Sensus.IntegrationTests.Infrastructure;
using Xunit;

namespace Sensus.IntegrationTests.Middlewares;

public class CorrelationIdMiddlewareTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public CorrelationIdMiddlewareTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Request_WhenCorrelationIdHeaderPresent_EchoesSameCorrelationId()
    {
        // Arrange
        var customCorrelationId = "test-custom-correlation-12345";
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        request.Headers.Add(CorrelationIdMiddleware.HeaderKey, customCorrelationId);

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.Should().ContainKey(CorrelationIdMiddleware.HeaderKey);
        response.Headers.GetValues(CorrelationIdMiddleware.HeaderKey).First().Should().Be(customCorrelationId);
    }

    [Fact]
    public async Task Request_WhenNoCorrelationIdHeader_GeneratesAndReturnsValidGuid()
    {
        // Arrange
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health");

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.Should().ContainKey(CorrelationIdMiddleware.HeaderKey);
        var correlationId = response.Headers.GetValues(CorrelationIdMiddleware.HeaderKey).First();
        Guid.TryParse(correlationId, out _).Should().BeTrue();
    }
}
