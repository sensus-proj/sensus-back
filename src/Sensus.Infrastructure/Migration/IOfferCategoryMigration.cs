namespace Sensus.Infrastructure.Migration;

/// <summary>
/// Migración única de categorías legadas de ofertas (Spec 034, FR-034-09). Se expone como interfaz para que el
/// servicio de arranque de la API (T034-15) pueda probarse sin MongoDB.
/// </summary>
public interface IOfferCategoryMigration
{
    /// <summary>
    /// Ejecuta la migración si no se ha ejecutado antes; en caso contrario devuelve la evidencia registrada.
    /// </summary>
    Task<OfferCategoryMigrationResult> ExecuteAsync(CancellationToken cancellationToken = default);
}
