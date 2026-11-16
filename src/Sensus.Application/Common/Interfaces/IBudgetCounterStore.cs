namespace Sensus.Application.Common.Interfaces;

/// <summary>
/// Almacén de contadores numéricos atómicos para el presupuesto mensual y la cuota diaria de Sensi IA
/// (Spec 049, EX-001). Toda operación que falle, expire o no esté configurada devuelve <c>null</c>; el
/// consumidor MUST tratarlo como indisponibilidad y degradar en modo <i>fail-closed</i>.
/// </summary>
public interface IBudgetCounterStore
{
    /// <summary>Indica si el almacén está configurado y puede intentar operar.</summary>
    bool IsAvailable { get; }

    /// <summary>
    /// Lee el valor del contador. Devuelve 0 si la clave no existe o ya expiró, y <c>null</c> ante cualquier fallo.
    /// </summary>
    Task<double?> GetAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Incrementa atómicamente el contador y fija su expiración la primera vez que se crea.
    /// Devuelve el nuevo total o <c>null</c> ante cualquier fallo.
    /// </summary>
    Task<double?> IncrementAsync(string key, double amount, TimeSpan expiration, CancellationToken cancellationToken = default);
}
