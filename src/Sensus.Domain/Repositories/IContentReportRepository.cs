using Sensus.Domain.Entities;
using Sensus.Domain.Enums;

namespace Sensus.Domain.Repositories;

/// <summary>
/// Repositorio para reportes de contenido comunitario con deduplicación y conteo por ventana (Spec 040).
/// </summary>
public interface IContentReportRepository
{
    Task<bool> ExistsRecentAsync(
        Guid reporterUserId,
        ReportTargetType targetType,
        Guid targetId,
        DateTime sinceUtc,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Inserta el reporte de forma idempotente dentro de la ventana única; devuelve false si hubo colisión de clave única.
    /// </summary>
    Task<bool> TryAddAsync(ContentReport report, CancellationToken cancellationToken = default);

    Task<int> CountRecentByTargetAsync(
        ReportTargetType targetType,
        Guid targetId,
        DateTime sinceUtc,
        CancellationToken cancellationToken = default);
}
