namespace Sensus.Application.Common.Settings;

/// <summary>
/// Un escalón de la escalera de sanciones. <see cref="Permanent"/> activo ignora <see cref="Hours"/>.
/// </summary>
public sealed class SanctionStepSetting
{
    public int? Hours { get; set; }

    public bool Permanent { get; set; }
}

/// <summary>
/// Configuración de sanciones (Spec 020, sección <c>Moderation:Sanctions</c>). Los valores viven en
/// configuración y nunca en código (Principio IV).
/// </summary>
public sealed class SanctionSettings
{
    public const string SectionName = "Moderation:Sanctions";

    public bool Enabled { get; set; } = true;

    /// <summary>Decisión de moderación que dispara la sanción. <c>Quarantine</c> no sanciona (D-3).</summary>
    public string TriggerDecision { get; set; } = "Rejected";

    public int EscalationWindowDays { get; set; } = 90;

    public List<SanctionStepSetting> Steps { get; set; } =
    [
        new() { Hours = 24 },
        new() { Hours = 168 },
        new() { Hours = 720 },
        new() { Permanent = true }
    ];
}
