using System.Text.Json;
using Microsoft.Extensions.Options;
using Sensus.Application.Common.Interfaces;

namespace Sensus.Infrastructure.Observability;

/// <summary>
/// Publica <c>SanctionsApplied</c> y <c>SanctionsReverted</c> con Embedded Metric Format (Spec 020, NFR-020-03).
/// Sin dimensiones: no incluye identificadores de usuario ni de sanción. Suma 2 métricas personalizadas a las 3
/// de la Spec 015, dentro del límite de 10 del nivel gratuito.
/// </summary>
public sealed class EmbeddedSanctionMetrics : ISanctionMetrics
{
    public const string AppliedMetric = "SanctionsApplied";
    public const string RevertedMetric = "SanctionsReverted";

    private readonly TextWriter _output;
    private readonly IDateTimeProvider _clock;
    private readonly string _namespace;

    public EmbeddedSanctionMetrics(IOptions<MetricsSettings> settings, IDateTimeProvider clock)
        : this(settings, clock, Console.Out)
    {
    }

    public EmbeddedSanctionMetrics(IOptions<MetricsSettings> settings, IDateTimeProvider clock, TextWriter output)
    {
        _namespace = string.IsNullOrWhiteSpace(settings.Value.Namespace) ? "Sensus/Api" : settings.Value.Namespace;
        _clock = clock;
        _output = output;
    }

    public void SanctionApplied(bool automatic) => Write(AppliedMetric);

    public void SanctionReverted() => Write(RevertedMetric);

    public string BuildPayload(string metricName)
    {
        var payload = new Dictionary<string, object>
        {
            ["_aws"] = new Dictionary<string, object>
            {
                ["Timestamp"] = new DateTimeOffset(_clock.UtcNow).ToUnixTimeMilliseconds(),
                ["CloudWatchMetrics"] = new[]
                {
                    new Dictionary<string, object>
                    {
                        ["Namespace"] = _namespace,
                        ["Dimensions"] = new[] { Array.Empty<string>() },
                        ["Metrics"] = new[]
                        {
                            new Dictionary<string, object> { ["Name"] = metricName, ["Unit"] = "Count" }
                        }
                    }
                }
            },
            [metricName] = 1
        };

        return JsonSerializer.Serialize(payload);
    }

    private void Write(string metricName) => _output.WriteLine(BuildPayload(metricName));
}

/// <summary>Implementación nula para entornos con las métricas desactivadas.</summary>
public sealed class NullSanctionMetrics : ISanctionMetrics
{
    public void SanctionApplied(bool automatic)
    {
    }

    public void SanctionReverted()
    {
    }
}
