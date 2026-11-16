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
    public async Task RateLimiter_WhenExceedingLimit_Returns429WithRetryAfterAndProblemDetails()
    {
        // Arrange
        var testIp = "198.51.100.42";
        var correlationId = "corr-rate-limit-test-429";

        var user = User.Create(
            "Rate Limit Test User",
            "rl.user@sensus.com",
            "hashed-password",
            true,
            DateTime.UtcNow).Value;

        _factory.UserRepository.GetByEmailAsync("rl.user@sensus.com", Arg.Any<CancellationToken>())
            .Returns(user);

        var command = new RequestPasswordResetCommand("rl.user@sensus.com");

        // Act - Recover policy allows 5 requests per window (configured in RateLimitingSettings:Recover: 5 requests)
        HttpResponseMessage? lastResponse = null;
        for (var i = 0; i < 6; i++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/recover")
            {
                Content = JsonContent.Create(command)
            };
            request.Headers.Add("X-Forwarded-For", testIp);
            request.Headers.Add("X-Correlation-Id", correlationId);

            lastResponse = await _client.SendAsync(request);
            if (i < 5)
            {
                lastResponse.StatusCode.Should().Be(HttpStatusCode.Accepted);
            }
        }

        // Assert
        lastResponse.Should().NotBeNull();
        lastResponse!.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        lastResponse.Headers.Contains("Retry-After").Should().BeTrue();

        var body = await lastResponse.Content.ReadAsStringAsync();
        using var jsonDoc = JsonDocument.Parse(body);
        var root = jsonDoc.RootElement;

        root.GetProperty("status").GetInt32().Should().Be(429);
        root.GetProperty("title").GetString().Should().Be("Too Many Requests");
        root.GetProperty("correlationId").GetString().Should().Be(correlationId);
    }

    [Fact]
    public async Task RateLimiter_WhenDifferentClientIp_HasSeparateQuotas()
    {
        // Arrange
        var ipA = "198.51.100.51";
        var ipB = "198.51.100.52";

        var user = User.Create(
            "Separate Quota User",
            "quota.user@sensus.com",
            "hashed-password",
            true,
            DateTime.UtcNow).Value;

        _factory.UserRepository.GetByEmailAsync("quota.user@sensus.com", Arg.Any<CancellationToken>())
            .Returns(user);

        var command = new RequestPasswordResetCommand("quota.user@sensus.com");

        // Exhaust IP A
        for (var i = 0; i < 5; i++)
        {
            using var reqA = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/recover")
            {
                Content = JsonContent.Create(command)
            };
            reqA.Headers.Add("X-Forwarded-For", ipA);
            var resA = await _client.SendAsync(reqA);
            resA.StatusCode.Should().Be(HttpStatusCode.Accepted);
        }

        // IP A 6th request is blocked
        using var reqABlocked = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/recover")
        {
            Content = JsonContent.Create(command)
        };
        reqABlocked.Headers.Add("X-Forwarded-For", ipA);
        var resABlocked = await _client.SendAsync(reqABlocked);
        resABlocked.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);

        // IP B should still be allowed
        using var reqB = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/recover")
        {
            Content = JsonContent.Create(command)
        };
        reqB.Headers.Add("X-Forwarded-For", ipB);
        var resB = await _client.SendAsync(reqB);
        resB.StatusCode.Should().Be(HttpStatusCode.Accepted);
    }
}
