namespace Sensus.Domain.ValueObjects;

/// <summary>
/// Evidencia de una sanción automática (Spec 020, FR-020-03). Solo contiene metadatos técnicos de la
/// decisión de moderación: nunca el contenido moderado ni datos personales.
/// </summary>
public sealed record SanctionEvidence(
    Guid? MediaId,
    string Decision,
    IReadOnlyList<string> Categories,
    IReadOnlyDictionary<string, double> Scores,
    string? ThresholdApplied,
    string Action)
{
    /// <summary>Evidencia vacía para sanciones manuales sin activo asociado.</summary>
    public static SanctionEvidence Manual { get; } = new(
        null,
        "Manual",
        [],
        new Dictionary<string, double>(),
        null,
        "ManualSanction");
}
