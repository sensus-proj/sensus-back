using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using NSubstitute;
using Sensus.Application.Users.Commands.RequestPasswordReset;
using Sensus.Domain.Entities;
using Sensus.IntegrationTests.Infrastructure;

namespace Sensus.IntegrationTests.Endpoints;

public class RateLimitingTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public RateLimitingTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task RateLimiter_WhenExceedingRecoverLimit_Returns429WithRetryAfterAndProblemDetails()
    {
        var email = "rl.user@sensus.com";
        var correlationId = "corr-rate-limit-test-429";
        var user = User.Create("Rate Limit Test User", email, "hashed-password", true, DateTime.UtcNow).Value;
        _factory.UserRepository.GetByEmailAsync(email, Arg.Any<CancellationToken>()).Returns(user);

        var command = new RequestPasswordResetCommand(email);

        // NFR-029-03: 3 solicitudes por correo y hora; la cuarta debe ser rechazada.
        HttpResponseMessage? lastResponse = null;
        for (var i = 0; i < 4; i++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/recover")
            {
                Content = JsonContent.Create(command)
            };
            request.Headers.Add("X-Forwarded-For", "198.51.100.42");
            request.Headers.Add("X-Correlation-Id", correlationId);

            lastResponse = await _client.SendAsync(request);
            if (i < 3)
            {
                lastResponse.StatusCode.Should().Be(HttpStatusCode.Accepted);
            }
        }

        lastResponse.Should().NotBeNull();
        lastResponse!.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        lastResponse.Headers.Contains("Retry-After").Should().BeTrue();

        using var jsonDoc = JsonDocument.Parse(await lastResponse.Content.ReadAsStringAsync());
        var root = jsonDoc.RootElement;
        root.GetProperty("status").GetInt32().Should().Be(429);
        root.GetProperty("title").GetString().Should().Be("Too Many Requests");
        root.GetProperty("correlationId").GetString().Should().Be(correlationId);
    }

    [Fact]
    public async Task RateLimiter_WhenDifferentEmail_HasSeparateQuotas()
    {
        var emailA = "quota.a@sensus.com";
        var emailB = "quota.b@sensus.com";

        for (var i = 0; i < 3; i++)
        {
            var responseA = await _client.PostAsJsonAsync(
                "/api/v1/auth/recover",
                new RequestPasswordResetCommand(emailA));
            responseA.StatusCode.Should().Be(HttpStatusCode.Accepted);
        }

        var blockedA = await _client.PostAsJsonAsync(
            "/api/v1/auth/recover",
            new RequestPasswordResetCommand(emailA));
        blockedA.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);

        var responseB = await _client.PostAsJsonAsync(
            "/api/v1/auth/recover",
            new RequestPasswordResetCommand(emailB));
        responseB.StatusCode.Should().Be(HttpStatusCode.Accepted);
    }

    [Fact]
    public async Task RateLimiter_SameEmailFromDifferentIp_SharesQuota()
    {
        var email = "quota.shared.ip@sensus.com";

        for (var i = 0; i < 3; i++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/recover")
            {
                Content = JsonContent.Create(new RequestPasswordResetCommand(email))
            };
            request.Headers.Add("X-Forwarded-For", i % 2 == 0 ? "198.51.100.61" : "198.51.100.62");

            var response = await _client.SendAsync(request);
            response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        }

        using var blockedRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/recover")
        {
            Content = JsonContent.Create(new RequestPasswordResetCommand(email))
        };
        blockedRequest.Headers.Add("X-Forwarded-For", "198.51.100.63");

        var blocked = await _client.SendAsync(blockedRequest);
        blocked.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }
}
