using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Common.Settings;
using Sensus.Domain.Enums;

namespace Sensus.Infrastructure.Observability;

/// <summary>
/// Publica métricas de interacciones y capacidad en Atlas M0 sin PII ni seudónimos (Spec 055).
/// Por defecto mantiene el clúster dentro de las 10 métricas personalizadas gratuitas de CloudWatch
/// (10/10 usando <c>InteractionStorageMaxPct</c>) y emite desgloses estructurados a CloudWatch Logs;
/// si se aprueba costo adicional (<see cref="InteractionSettings.EmitDetailedCustomMetrics"/>), emite
/// las métricas EMF individuales.
/// </summary>
public sealed class EmbeddedInteractionMetricsPublisher : IInteractionMetricsPublisher
{
    public const string StorageMaxPercentMetric = "InteractionStorageMaxPct";
    public const string WriteFailedMetric = "InteractionWriteFailed";
    public const string WrittenMetric = "InteractionsWritten";
    public const string OpenRateMetric = "InteractionOpenRate";
    public const string ClaimRateMetric = "InteractionClaimRate";
    public const string CollectionBytesMetric = "OfferInteractionsBytes";
    public const string ClusterBytesMetric = "MongoClusterBytes";

    private readonly string _namespace;
    private readonly InteractionSettings _interactionSettings;
    private readonly IDateTimeProvider _clock;
    private readonly ILogger<EmbeddedInteractionMetricsPublisher> _logger;
    private readonly TextWriter _output;

    public EmbeddedInteractionMetricsPublisher(
        IOptions<MetricsSettings> metricsSettings,
        IOptions<InteractionSettings> interactionSettings,
        IDateTimeProvider clock,
        ILogger<EmbeddedInteractionMetricsPublisher> logger)
        : this(metricsSettings, interactionSettings, clock, logger, Console.Out)
    {
    }

    public EmbeddedInteractionMetricsPublisher(
        IOptions<MetricsSettings> metricsSettings,
        IOptions<InteractionSettings> interactionSettings,
        IDateTimeProvider clock,
        ILogger<EmbeddedInteractionMetricsPublisher> logger,
        TextWriter output)
    {
        _namespace = string.IsNullOrWhiteSpace(metricsSettings.Value.Namespace) ? "Sensus/Api" : metricsSettings.Value.Namespace;
        _interactionSettings = interactionSettings.Value;
        _clock = clock;
        _logger = logger;
        _output = output;
    }

    public void RecordWritten(OfferInteractionType type)
    {
        if (_interactionSettings.EmitDetailedCustomMetrics)
        {
            _output.WriteLine(BuildCountEmfPayload(WrittenMetric, type.ToWireName()));
        }
        else
        {
            _logger.LogDebug("Interacción registrada ({InteractionType}).", type.ToWireName());
        }
    }

    public void RecordWriteFailure(OfferInteractionType type)
    {
        if (_interactionSettings.EmitDetailedCustomMetrics)
        {
            _output.WriteLine(BuildCountEmfPayload(WriteFailedMetric, type.ToWireName()));
        }
        else
        {
            _logger.LogWarning("Metric={MetricName} InteractionType={InteractionType} Count=1", WriteFailedMetric, type.ToWireName());
        }
    }

    public void PublishDailyQuality(IReadOnlyList<InteractionDailyQualityMetric> metrics)
    {
        ArgumentNullException.ThrowIfNull(metrics);

        foreach (InteractionDailyQualityMetric row in metrics)
        {
            if (_interactionSettings.EmitDetailedCustomMetrics)
            {
                _output.WriteLine(BuildQualityEmfPayload(row));
            }
            else
            {
                _logger.LogInformation(
                    "Metric=InteractionDailyQuality RankingVersion={RankingVersion} Slot={Slot} Impressions={Impressions} Opens={Opens} Claims={Claims} OpenRate={OpenRate} ClaimRate={ClaimRate}",
                    row.RankingVersion,
                    row.Slot.ToWireName(),
                    row.Impressions,
                    row.Opens,
                    row.Claims,
                    row.OpenRate,
                    row.ClaimRate);
            }
        }
    }

    public void PublishStorageUtilization(InteractionStorageStats stats)
    {
        ArgumentNullException.ThrowIfNull(stats);

        _output.WriteLine(BuildStorageEmfPayload(stats));

        if (stats.MaxUtilizationPercent >= _interactionSettings.AlarmThresholdPercent)
        {
            _logger.LogWarning(
                "Capacidad Atlas M0 alcanzó {MaxPct}% (colección={CollPct}%, clúster={ClusterPct}%).",
                stats.MaxUtilizationPercent,
                stats.CollectionUtilizationPercent,
                stats.ClusterUtilizationPercent);
        }
    }

    public string BuildStorageEmfPayload(InteractionStorageStats stats)
    {
        long timestamp = new DateTimeOffset(_clock.UtcNow).ToUnixTimeMilliseconds();
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
            WriteMetricDefinition(writer, StorageMaxPercentMetric, "Percent");
            if (_interactionSettings.EmitDetailedCustomMetrics)
            {
                WriteMetricDefinition(writer, CollectionBytesMetric, "Bytes");
                WriteMetricDefinition(writer, ClusterBytesMetric, "Bytes");
            }
            writer.WriteEndArray();
            writer.WriteEndObject();
            writer.WriteEndArray();
            writer.WriteEndObject();

            writer.WriteNumber(StorageMaxPercentMetric, stats.MaxUtilizationPercent);
            writer.WriteNumber(CollectionBytesMetric, stats.CollectionSizeBytes);
            writer.WriteNumber(ClusterBytesMetric, stats.ClusterSizeBytes);
            writer.WriteNumber("CollectionUtilizationPercent", stats.CollectionUtilizationPercent);
            writer.WriteNumber("ClusterUtilizationPercent", stats.ClusterUtilizationPercent);
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    private string BuildCountEmfPayload(string metricName, string typeName)
    {
        long timestamp = new DateTimeOffset(_clock.UtcNow).ToUnixTimeMilliseconds();
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
            writer.WriteStringValue("InteractionType");
            writer.WriteEndArray();
            writer.WriteEndArray();
            writer.WriteStartArray("Metrics");
            WriteMetricDefinition(writer, metricName, "Count");
            writer.WriteEndArray();
            writer.WriteEndObject();
            writer.WriteEndArray();
            writer.WriteEndObject();

            writer.WriteString("InteractionType", typeName);
            writer.WriteNumber(metricName, 1);
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    private string BuildQualityEmfPayload(InteractionDailyQualityMetric row)
    {
        long timestamp = new DateTimeOffset(_clock.UtcNow).ToUnixTimeMilliseconds();
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
            writer.WriteStringValue("RankingVersion");
            writer.WriteStringValue("Slot");
            writer.WriteEndArray();
            writer.WriteEndArray();
            writer.WriteStartArray("Metrics");
            WriteMetricDefinition(writer, OpenRateMetric, "None");
            WriteMetricDefinition(writer, ClaimRateMetric, "None");
            writer.WriteEndArray();
            writer.WriteEndObject();
            writer.WriteEndArray();
            writer.WriteEndObject();

            writer.WriteString("RankingVersion", row.RankingVersion);
            writer.WriteString("Slot", row.Slot.ToWireName());
            writer.WriteNumber(OpenRateMetric, row.OpenRate);
            writer.WriteNumber(ClaimRateMetric, row.ClaimRate);
            writer.WriteNumber("Impressions", row.Impressions);
            writer.WriteNumber("Opens", row.Opens);
            writer.WriteNumber("Claims", row.Claims);
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
/// Publicador nulo para entornos con métricas deshabilitadas.
/// </summary>
public sealed class NullInteractionMetricsPublisher : IInteractionMetricsPublisher
{
    public void RecordWritten(OfferInteractionType type)
    {
    }

    public void RecordWriteFailure(OfferInteractionType type)
    {
    }

    public void PublishDailyQuality(IReadOnlyList<InteractionDailyQualityMetric> metrics)
    {
    }

    public void PublishStorageUtilization(InteractionStorageStats stats)
    {
    }
}
