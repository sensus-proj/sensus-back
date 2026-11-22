using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Sensus.Application.Common.Interfaces;
using Sensus.Infrastructure.Observability;

namespace Sensus.IntegrationTests.Observability;

/// <summary>
/// Pruebas de emisión EMF para Sensi IA (Spec 046, FR-046-14, NFR-046-06).
/// </summary>
public sealed class AiMetricsTests
{
    private static readonly DateTime Now = new(2026, 11, 19, 15, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void BuildPayload_ShouldContainAllFourMetricsAndNoDimensionsOrPii()
    {
        var publisher = CreatePublisher(TextWriter.Null, "Sensus/Api");
        var metric = new AiTurnMetricEvent(
            LatencyMs: 345.678,
            CachedTokens: 1200,
            EstimatedCostUsd: 0.0015m,
            DegradedOrBlocked: false,
            RoutingTier: "Fast",
            FaqCacheHit: false,
            CorrelationId: "corr-abc-123");

        string payload = publisher.BuildPayload(metric);

        using JsonDocument doc = JsonDocument.Parse(payload);
        JsonElement root = doc.RootElement;
        JsonElement aws = root.GetProperty("_aws");
        aws.GetProperty("Timestamp").GetInt64().Should().Be(new DateTimeOffset(Now).ToUnixTimeMilliseconds());

        JsonElement directive = aws.GetProperty("CloudWatchMetrics")[0];
        directive.GetProperty("Namespace").GetString().Should().Be("Sensus/Api");
        directive.GetProperty("Dimensions").GetArrayLength().Should().Be(1);
        directive.GetProperty("Dimensions")[0].GetArrayLength().Should().Be(0);

        var definitions = directive.GetProperty("Metrics").EnumerateArray()
            .ToDictionary(m => m.GetProperty("Name").GetString()!, m => m.GetProperty("Unit").GetString()!);
        definitions.Should().BeEquivalentTo(new Dictionary<string, string>
        {
            [EmbeddedAiMetricsPublisher.TurnLatencyMetric] = "Milliseconds",
            [EmbeddedAiMetricsPublisher.CachedTokensMetric] = "Count",
            [EmbeddedAiMetricsPublisher.EstimatedCostMicroUsdMetric] = "Count",
            [EmbeddedAiMetricsPublisher.FallbackOrBlockedMetric] = "Count"
        });

        root.GetProperty(EmbeddedAiMetricsPublisher.TurnLatencyMetric).GetDouble().Should().Be(345.68);
        root.GetProperty(EmbeddedAiMetricsPublisher.CachedTokensMetric).GetInt32().Should().Be(1200);
        root.GetProperty(EmbeddedAiMetricsPublisher.EstimatedCostMicroUsdMetric).GetDecimal().Should().Be(1500m);
        root.GetProperty(EmbeddedAiMetricsPublisher.FallbackOrBlockedMetric).GetInt32().Should().Be(0);
        root.GetProperty("CorrelationId").GetString().Should().Be("corr-abc-123");
        root.GetProperty("RoutingTier").GetString().Should().Be("Fast");
        root.GetProperty("FaqCacheHit").GetBoolean().Should().BeFalse();

        root.EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo(
            "_aws",
            EmbeddedAiMetricsPublisher.TurnLatencyMetric,
            EmbeddedAiMetricsPublisher.CachedTokensMetric,
            EmbeddedAiMetricsPublisher.EstimatedCostMicroUsdMetric,
            EmbeddedAiMetricsPublisher.FallbackOrBlockedMetric,
            "CorrelationId",
            "RoutingTier",
            "FaqCacheHit");
    }

    [Fact]
    public void BuildPayload_ShouldFlagDegradedOrBlocked_AndClampNegativeValues()
    {
        var publisher = CreatePublisher(TextWriter.Null, " ");
        var metric = new AiTurnMetricEvent(
            LatencyMs: -10,
            CachedTokens: -5,
            EstimatedCostUsd: -1m,
            DegradedOrBlocked: true,
            RoutingTier: "Blocked",
            FaqCacheHit: true,
            CorrelationId: "corr-blocked");

        using JsonDocument doc = JsonDocument.Parse(publisher.BuildPayload(metric));
        JsonElement root = doc.RootElement;

        root.GetProperty("_aws").GetProperty("CloudWatchMetrics")[0]
            .GetProperty("Namespace").GetString().Should().Be("Sensus/Api");
        root.GetProperty(EmbeddedAiMetricsPublisher.TurnLatencyMetric).GetDouble().Should().Be(0);
        root.GetProperty(EmbeddedAiMetricsPublisher.CachedTokensMetric).GetInt32().Should().Be(0);
        root.GetProperty(EmbeddedAiMetricsPublisher.EstimatedCostMicroUsdMetric).GetDecimal().Should().Be(0m);
        root.GetProperty(EmbeddedAiMetricsPublisher.FallbackOrBlockedMetric).GetInt32().Should().Be(1);
        root.GetProperty("FaqCacheHit").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public void PublishTurn_ShouldWriteSingleJsonLineToOutput_AndNullPublisherShouldNotThrow()
    {
        using var output = new StringWriter();
        var publisher = CreatePublisher(output, "Sensus/Test");
        var metric = new AiTurnMetricEvent(12, 0, 0m, false, "Fast", true, "corr-1");

        publisher.PublishTurn(metric);

        string written = output.ToString();
        string[] lines = written.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        lines.Should().ContainSingle();
        using JsonDocument doc = JsonDocument.Parse(lines[0]);
        doc.RootElement.GetProperty("_aws").GetProperty("CloudWatchMetrics")[0]
            .GetProperty("Namespace").GetString().Should().Be("Sensus/Test");

        var nullPublisher = new NullAiMetricsPublisher();
        Action act = () => nullPublisher.PublishTurn(metric);
        act.Should().NotThrow();
    }

    private static EmbeddedAiMetricsPublisher CreatePublisher(TextWriter output, string metricsNamespace)
    {
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(Now);
        return new EmbeddedAiMetricsPublisher(
            Options.Create(new MetricsSettings { Enabled = true, Namespace = metricsNamespace }),
            clock,
            output);
    }
}
