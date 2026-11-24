namespace Sensus.Application.Common.Settings;

/// <summary>
/// Configuración del registro seudonimizado de interacciones con ofertas (Spec 055).
/// Las claves HMAC reales se cargan exclusivamente desde SSM Parameter Store en producción.
/// </summary>
public sealed class InteractionSettings
{
    public const string SectionName = "Interactions";

    /// <summary>Habilita o deshabilita el registro de interacciones.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Fracción muestreada [0..1] únicamente para impresiones del feed (plan §6). <c>open</c> y <c>claim</c> nunca se muestrean.</summary>
    public double ImpressionSampleRate { get; set; } = 1.0;

    /// <summary>Retención en días usada para configurar el índice TTL en MongoDB (180 por defecto).</summary>
    public int RetentionDays { get; set; } = 180;

    /// <summary>Tiempo máximo de espera por escritura de mejor esfuerzo antes de degradar sin afectar la operación.</summary>
    public int WriteTimeoutMs { get; set; } = 200;

    /// <summary>Versión activa de ranking asignada al feed (FR-055-01).</summary>
    public string RankingVersion { get; set; } = "feed-v1";

    /// <summary>Versión activa de clave HMAC para seudonimizar nuevos eventos (FR-055-04).</summary>
    public int ActiveKeyVersion { get; set; } = 1;

    /// <summary>Mapa versión → clave HMAC en Base64 (mínimo 32 bytes tras decodificar), inyectado desde SSM en despliegue.</summary>
    public Dictionary<int, string> HmacKeys { get; set; } = new();

    /// <summary>Cadena JSON opcional con mapa versión → clave Base64 proveniente de un único parámetro SSM <c>SecureString</c>.</summary>
    public string? HmacKeysJson { get; set; }

    /// <summary>Tope de almacenamiento de la colección <c>offer_interactions</c> en Atlas M0 (150 MB, NFR-055-04).</summary>
    public long CollectionLimitBytes { get; set; } = 150L * 1024 * 1024;

    /// <summary>Tope del clúster Atlas M0 compartido (512 MB, NFR-055-04).</summary>
    public long ClusterLimitBytes { get; set; } = 512L * 1024 * 1024;

    /// <summary>Umbral porcentual de alerta de capacidad (70 %, T055-08).</summary>
    public int AlarmThresholdPercent { get; set; } = 70;

    /// <summary>
    /// Cuando es <c>false</c> (por defecto Free Tier), CloudWatch EMF emite únicamente la métrica combinada de utilización
    /// (<c>InteractionStorageMaxPct</c>, 10/10 métricas del nivel gratuito) y registra los desgloses como logs JSON estructurados.
    /// Solo activar a <c>true</c> previa aprobación explícita de métricas personalizadas adicionales.
    /// </summary>
    public bool EmitDetailedCustomMetrics { get; set; }
}
