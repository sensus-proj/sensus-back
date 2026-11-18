namespace Sensus.Infrastructure.Ai.Budget;

/// <summary>
/// Configuración del almacén de contadores de presupuesto de Sensi IA en DynamoDB (Spec 049).
/// El nombre de tabla llega por <c>AiBudgetStore__TableName</c>; no contiene secretos.
/// </summary>
public sealed class AiBudgetStoreSettings
{
    public const string SectionName = "AiBudgetStore";

    /// <summary>Nombre de la tabla DynamoDB. Vacío deshabilita el almacén (fail-closed).</summary>
    public string TableName { get; set; } = string.Empty;

    /// <summary>Endpoint opcional para DynamoDB Local en desarrollo.</summary>
    public string? ServiceUrl { get; set; }

    /// <summary>Tiempo máximo por operación, en segundos.</summary>
    public int TimeoutSeconds { get; set; } = 3;
}
