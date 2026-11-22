namespace Sensus.Domain.Policies;

/// <summary>
/// Escalón de la escalera de sanciones. <see cref="Hours"/> nulo significa baneo permanente.
/// </summary>
public sealed record SanctionStep(int? Hours)
{
    public bool IsPermanent => Hours is null;
}

/// <summary>
/// Política pura de escalamiento por reincidencia (Spec 020, FR-020-06): la duración de la sanción depende de
/// cuántas sanciones vigentes o vencidas, no revertidas, tiene el usuario dentro de la ventana configurada.
/// No contiene valores fijos: los escalones llegan por configuración.
/// </summary>
public static class SanctionEscalationPolicy
{
    /// <summary>
    /// Devuelve el escalón correspondiente a una nueva sanción. Si el historial supera la escalera se
    /// mantiene el último escalón, que por diseño es el más severo.
    /// </summary>
    public static SanctionStep Resolve(int priorSanctionsInWindow, IReadOnlyList<SanctionStep> steps)
    {
        ArgumentNullException.ThrowIfNull(steps);
        if (steps.Count == 0)
        {
            throw new ArgumentException("La escalera de sanciones no puede estar vacía.", nameof(steps));
        }

        int index = Math.Clamp(priorSanctionsInWindow, 0, steps.Count - 1);
        return steps[index];
    }
}
