namespace Sensus.Infrastructure.Services;

public sealed class MailerSettings
{
    public const string SectionName = "MailerSettings";

    public string BaseUrl { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public int TimeoutSeconds { get; set; } = 5;

    /// <summary>
    /// Único host al que se permite enviar por HTTP sin TLS (excepción EX-004, FR-029-10).
    /// Vacío: solo HTTPS.
    /// </summary>
    public string InsecureHttpAllowedHost { get; set; } = string.Empty;

    /// <summary>
    /// Fecha (UTC) en la que vence el permiso de HTTP sin TLS. Sin fecha o vencida: solo HTTPS.
    /// </summary>
    public DateTimeOffset? InsecureHttpAllowedUntil { get; set; }
}
