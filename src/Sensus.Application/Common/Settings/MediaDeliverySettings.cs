namespace Sensus.Application.Common.Settings;

/// <summary>
/// Configuración de la entrega de contenido multimedia por CDN (Spec 023).
/// Ref: FR-023-02 - firma de URLs; FR-023-03 - cabeceras de caché acotadas por vigencia.
/// </summary>
public sealed class MediaDeliverySettings
{
    public const string SectionName = "MediaDelivery";

    /// <summary>Clave HMAC-SHA256 en Base64. Nunca se versiona; se resuelve por entorno o SSM.</summary>
    public string SigningKey { get; set; } = string.Empty;

    /// <summary>Base pública de la API usada para construir la URL firmada.</summary>
    public string PublicBaseUrl { get; set; } = "https://localhost:5001";

    /// <summary>Vigencia de la URL firmada para contenido aprobado (público), en minutos.</summary>
    public int PublicTtlMinutes { get; set; } = 60;

    /// <summary>Vigencia de la URL firmada para contenido privado, en minutos.</summary>
    public int PrivateTtlMinutes { get; set; } = 5;

    /// <summary>Max-Age de caché para contenido público aprobado, en segundos.</summary>
    public int PublicCacheSeconds { get; set; } = 3600;

    /// <summary>Timeout para la lectura del origen multimedia por proxy, en segundos.</summary>
    public int ProxyTimeoutSeconds { get; set; } = 15;

    /// <summary>Tamaño máximo de respuesta permitido por el proxy en bytes (por defecto 10 MB).</summary>
    public long MaxResponseSizeBytes { get; set; } = 10 * 1024 * 1024;
}
