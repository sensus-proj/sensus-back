namespace Sensus.Infrastructure.Observability;

/// <summary>
/// Configuración de las métricas de aplicación en CloudWatch (Spec 015).
/// </summary>
public sealed class MetricsSettings
{
    public const string SectionName = "Observability:Metrics";

    /// <summary>Activa la emisión en formato EMF. En local queda desactivada para no ensuciar la consola.</summary>
    public bool Enabled { get; set; }

    /// <summary>Espacio de nombres de CloudWatch donde se publican las métricas.</summary>
    public string Namespace { get; set; } = "Sensus/Api";
}
