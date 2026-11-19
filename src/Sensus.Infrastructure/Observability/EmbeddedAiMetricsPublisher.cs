using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Sensus.Application.Common.Interfaces;

namespace Sensus.Infrastructure.Observability;

/// <summary>
/// Publica métricas de Sensi IA en CloudWatch mediante Embedded Metric Format (Spec 046, FR-046-14, NFR-046-06).
/// Emite cuatro métricas sin dimensiones (<c>AiTurnLatency</c>, <c>AiCachedTokens</c>, <c>AiEstimatedCostMicroUsd</c>
/// y <c>AiFallbackOrBlocked</c>) sin PII ni texto de prompts/respuestas, manteniéndose dentro de las 10 métricas
/// personalizadas del nivel gratuito de CloudWatch (9/10 en total).
/// </summary>
public sealed class EmbeddedAiMetricsPublisher : IAiMetricsPublisher
{
    public const string TurnLatencyMetric = "AiTurnLatency";
    public const string CachedTokensMetric = "AiCachedTokens";
    public const string EstimatedCostMicroUsdMetric = "AiEstimatedCostMicroUsd";
    public const string FallbackOrBlockedMetric = "AiFallbackOrBlocked";

    private readonly TextWriter _output;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly string _namespace;

    public EmbeddedAiMetricsPublisher(IOptions<MetricsSettings> settings, IDateTimeProvider dateTimeProvider)
        : this(settings, dateTimeProvider, Console.Out)
    {
    }

    public EmbeddedAiMetricsPublisher(
        IOptions<MetricsSettings> settings,
        IDateTimeProvider dateTimeProvider,
        TextWriter output)
    {
        _namespace = string.IsNullOrWhiteSpace(settings.Value.Namespace) ? "Sensus/Api" : settings.Value.Namespace;
        _dateTimeProvider = dateTimeProvider;
        _output = output;
    }

    public void PublishTurn(AiTurnMetricEvent metric)
    {
        ArgumentNullException.ThrowIfNull(metric);
        _output.WriteLine(BuildPayload(metric));
    }

    public string BuildPayload(AiTurnMetricEvent metric)
    {
        ArgumentNullException.ThrowIfNull(metric);

        long timestamp = new DateTimeOffset(_dateTimeProvider.UtcNow).ToUnixTimeMilliseconds();
        double latencyMs = Math.Round(Math.Max(metric.LatencyMs, 0), 2);
        int cachedTokens = Math.Max(metric.CachedTokens, 0);
        decimal microUsd = Math.Round(Math.Max(metric.EstimatedCostUsd, 0m) * 1_000_000m, 2);

        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();

            writer.WriteStartObject("_aws");
            writer.WriteNumber("Timestamp", timestamp);
            writer.WriteStartArray("CloudWatchMetrics");
            writer.WriteStartObject();
            writer.WriteString("Namespace", _namespace);
            writer.WriteStartArray("Dimensions");
            writer.WriteStartArray();
            writer.WriteEndArray();
            writer.WriteEndArray();
            writer.WriteStartArray("Metrics");
            WriteMetricDefinition(writer, TurnLatencyMetric, "Milliseconds");
            WriteMetricDefinition(writer, CachedTokensMetric, "Count");
            WriteMetricDefinition(writer, EstimatedCostMicroUsdMetric, "Count");
            WriteMetricDefinition(writer, FallbackOrBlockedMetric, "Count");
            writer.WriteEndArray();
            writer.WriteEndObject();
            writer.WriteEndArray();
            writer.WriteEndObject();

            writer.WriteNumber(TurnLatencyMetric, latencyMs);
            writer.WriteNumber(CachedTokensMetric, cachedTokens);
            writer.WriteNumber(EstimatedCostMicroUsdMetric, microUsd);
            writer.WriteNumber(FallbackOrBlockedMetric, metric.DegradedOrBlocked ? 1 : 0);
            writer.WriteString("CorrelationId", metric.CorrelationId ?? string.Empty);
            writer.WriteString("RoutingTier", metric.RoutingTier ?? string.Empty);
            writer.WriteBoolean("FaqCacheHit", metric.FaqCacheHit);

            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    private static void WriteMetricDefinition(Utf8JsonWriter writer, string name, string unit)
    {
        writer.WriteStartObject();
        writer.WriteString("Name", name);
        writer.WriteString("Unit", unit);
        writer.WriteEndObject();
    }
}

/// <summary>
/// Publicador nulo para entornos con las métricas de CloudWatch desactivadas.
/// </summary>
public sealed class NullAiMetricsPublisher : IAiMetricsPublisher
{
    public void PublishTurn(AiTurnMetricEvent metric)
    {
    }
}
