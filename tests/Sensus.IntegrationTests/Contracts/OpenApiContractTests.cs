using System.Net;
using System.Text.Json;
using FluentAssertions;
using Sensus.IntegrationTests.Infrastructure;

namespace Sensus.IntegrationTests.Contracts;

public sealed class OpenApiContractTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public OpenApiContractTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task OpenApiDocument_IsExposedAtSingleEndpointWithBearerSecurityAndCurrentPaths()
    {
        HttpResponseMessage response = await _client.GetAsync("/openapi/v1.json");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        string json = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(json);
        JsonElement root = doc.RootElement;

        root.GetProperty("openapi").GetString().Should().StartWith("3.");
        root.GetProperty("components").GetProperty("securitySchemes").TryGetProperty("Bearer", out _).Should().BeTrue();

        JsonElement paths = root.GetProperty("paths");
        paths.TryGetProperty("/api/v1/auth/guest", out _).Should().BeTrue();
        paths.TryGetProperty("/api/v1/feed/posts/{id}/comments", out _).Should().BeTrue();
        paths.TryGetProperty("/api/v1/reports", out _).Should().BeTrue();
        paths.TryGetProperty("/api/v1/friends", out _).Should().BeTrue();
        paths.TryGetProperty("/api/v1/offers/{id}/republish", out _).Should().BeTrue();
    }
}
