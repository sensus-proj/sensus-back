using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Sensus.Application.Common.Interfaces;

namespace Sensus.Infrastructure.Observability;

/// <summary>
/// Publica métricas de aplicación en CloudWatch mediante Embedded Metric Format (EMF): cada solicitud escribe
/// una línea JSON en la salida estándar y CloudWatch Logs la convierte en métricas sin llamadas a la API
/// (FR-015-01). Se emiten tres métricas sin dimensiones (<c>RequestLatency</c>, <c>ServerErrors</c> y
/// <c>Throttled</c>) para quedar dentro de las 10 métricas personalizadas del Free Tier (NFR-015-01).
/// </summary>
public sealed class EmbeddedMetricsPublisher : IRequestMetricsPublisher
{
    public const string LatencyMetric = "RequestLatency";
    public const string ServerErrorsMetric = "ServerErrors";
    public const string ThrottledMetric = "Throttled";

    private readonly TextWriter _output;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly string _namespace;

    public EmbeddedMetricsPublisher(IOptions<MetricsSettings> settings, IDateTimeProvider dateTimeProvider)
        : this(settings, dateTimeProvider, Console.Out)
    {
    }

    public EmbeddedMetricsPublisher(
        IOptions<MetricsSettings> settings,
        IDateTimeProvider dateTimeProvider,
        TextWriter output)
    {
        _namespace = string.IsNullOrWhiteSpace(settings.Value.Namespace) ? "Sensus/Api" : settings.Value.Namespace;
        _dateTimeProvider = dateTimeProvider;
        _output = output;
    }

    public void PublishRequest(int statusCode, double elapsedMilliseconds)
    {
        _output.WriteLine(BuildPayload(statusCode, elapsedMilliseconds));
    }

    public string BuildPayload(int statusCode, double elapsedMilliseconds)
    {
        long timestamp = new DateTimeOffset(_dateTimeProvider.UtcNow).ToUnixTimeMilliseconds();

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
            WriteMetricDefinition(writer, LatencyMetric, "Milliseconds");
            WriteMetricDefinition(writer, ServerErrorsMetric, "Count");
            WriteMetricDefinition(writer, ThrottledMetric, "Count");
            writer.WriteEndArray();
            writer.WriteEndObject();
            writer.WriteEndArray();
            writer.WriteEndObject();

            writer.WriteNumber(LatencyMetric, Math.Round(Math.Max(elapsedMilliseconds, 0), 2));
            writer.WriteNumber(ServerErrorsMetric, statusCode >= 500 ? 1 : 0);
            writer.WriteNumber(ThrottledMetric, statusCode == 429 ? 1 : 0);
            writer.WriteNumber("StatusCode", statusCode);

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
/// Publicador vacío para entornos sin CloudWatch (desarrollo local y pruebas).
/// </summary>
public sealed class NoOpRequestMetricsPublisher : IRequestMetricsPublisher
{
    public void PublishRequest(int statusCode, double elapsedMilliseconds)
    {
    }
}
