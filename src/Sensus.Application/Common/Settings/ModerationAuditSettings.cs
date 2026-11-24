namespace Sensus.Application.Common.Settings;

/// <summary>
/// Configuración de la auditoría de moderación (Spec 021).
/// Ref: FR-021-04 - retención de la evidencia conforme a la política de datos.
/// </summary>
public sealed class ModerationAuditSettings
{
    public const string SectionName = "Moderation:Audit";

    /// <summary>Días que se conserva la evidencia antes de que la política TTL la purgue.</summary>
    public int RetentionDays { get; set; } = 2555;
}
