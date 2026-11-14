using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Sensus.Application.Common.Interfaces;
using Sensus.Infrastructure.Observability;
using Sensus.IntegrationTests.Infrastructure;
using Xunit;

namespace Sensus.IntegrationTests.Observability;

/// <summary>
/// Pruebas de las métricas de aplicación (Spec 015, FR-015-01).
/// </summary>
public class RequestMetricsTests : IClassFixture<CustomWebApplicationFactory>
{
    private static readonly DateTime Now = new(2026, 11, 13, 12, 0, 0, DateTimeKind.Utc);

    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public RequestMetricsTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Theory]
    [InlineData("/health/live", HttpStatusCode.OK)]
    [InlineData("/api/v1/users/profile", HttpStatusCode.Unauthorized)]
    [InlineData("/api/v1/ruta-que-no-existe", HttpStatusCode.NotFound)]
    public async Task Middleware_ShouldPublishFinalStatusCodeAndLatency(string route, HttpStatusCode expected)
    {
        int before = _factory.Metrics.Requests.Count(r => r.StatusCode == (int)expected);

        var response = await _client.GetAsync(route);

        response.StatusCode.Should().Be(expected);
        _factory.Metrics.Requests.Count(r => r.StatusCode == (int)expected).Should().BeGreaterThan(before);
        _factory.Metrics.Requests.Should().OnlyContain(r => r.ElapsedMilliseconds >= 0);
    }

    [Fact]
    public void EmbeddedPublisher_ShouldWriteValidEmfLine_ForServerError()
    {
        using var output = new StringWriter();
        var publisher = CreatePublisher(output, "Sensus/Test");

        publisher.PublishRequest(503, 120.456);

        using JsonDocument document = JsonDocument.Parse(output.ToString().Trim());
        JsonElement root = document.RootElement;
        JsonElement aws = root.GetProperty("_aws");
        aws.GetProperty("Timestamp").GetInt64().Should().Be(new DateTimeOffset(Now).ToUnixTimeMilliseconds());

        JsonElement directive = aws.GetProperty("CloudWatchMetrics")[0];
        directive.GetProperty("Namespace").GetString().Should().Be("Sensus/Test");
        directive.GetProperty("Dimensions")[0].GetArrayLength().Should().Be(0);
        directive.GetProperty("Metrics").EnumerateArray()
            .Select(m => m.GetProperty("Name").GetString())
            .Should().BeEquivalentTo(
                EmbeddedMetricsPublisher.LatencyMetric,
                EmbeddedMetricsPublisher.ServerErrorsMetric,
                EmbeddedMetricsPublisher.ThrottledMetric);

        root.GetProperty(EmbeddedMetricsPublisher.LatencyMetric).GetDouble().Should().Be(120.46);
        root.GetProperty(EmbeddedMetricsPublisher.ServerErrorsMetric).GetInt32().Should().Be(1);
        root.GetProperty(EmbeddedMetricsPublisher.ThrottledMetric).GetInt32().Should().Be(0);
        root.GetProperty("StatusCode").GetInt32().Should().Be(503);
    }

    [Theory]
    [InlineData(200, 0, 0)]
    [InlineData(429, 0, 1)]
    [InlineData(500, 1, 0)]
    public void EmbeddedPublisher_ShouldFlagErrorsAndThrottling(int statusCode, int serverErrors, int throttled)
    {
        var publisher = CreatePublisher(TextWriter.Null, "Sensus/Api");

        using JsonDocument document = JsonDocument.Parse(publisher.BuildPayload(statusCode, 10));

        document.RootElement.GetProperty(EmbeddedMetricsPublisher.ServerErrorsMetric).GetInt32().Should().Be(serverErrors);
        document.RootElement.GetProperty(EmbeddedMetricsPublisher.ThrottledMetric).GetInt32().Should().Be(throttled);
    }

    [Fact]
    public void EmbeddedPublisher_ShouldFallBackToDefaultNamespace_AndClampNegativeLatency()
    {
        var publisher = CreatePublisher(TextWriter.Null, " ");

        using JsonDocument document = JsonDocument.Parse(publisher.BuildPayload(200, -5));

        document.RootElement.GetProperty("_aws").GetProperty("CloudWatchMetrics")[0]
            .GetProperty("Namespace").GetString().Should().Be("Sensus/Api");
        document.RootElement.GetProperty(EmbeddedMetricsPublisher.LatencyMetric).GetDouble().Should().Be(0);
    }

    [Fact]
    public void EmbeddedPublisher_ShouldNotIncludeRoutesOrIdentifiers()
    {
        var publisher = CreatePublisher(TextWriter.Null, "Sensus/Api");

        string payload = publisher.BuildPayload(404, 3);

        using JsonDocument document = JsonDocument.Parse(payload);
        document.RootElement.EnumerateObject().Select(p => p.Name)
            .Should().BeEquivalentTo("_aws", "RequestLatency", "ServerErrors", "Throttled", "StatusCode");
    }

    private static EmbeddedMetricsPublisher CreatePublisher(TextWriter output, string metricsNamespace)
    {
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(Now);
        return new EmbeddedMetricsPublisher(
            Options.Create(new MetricsSettings { Enabled = true, Namespace = metricsNamespace }),
            clock,
            output);
    }
}
