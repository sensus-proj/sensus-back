using Sensus.Domain.Entities;
using Sensus.Domain.Enums;

namespace Sensus.Domain.Repositories;

/// <summary>
/// Repositorio de evidencia de moderación (Spec 021).
/// Deliberadamente no expone actualización ni borrado: el registro es de solo adición (NFR-021-01).
/// </summary>
public interface IModerationAuditRepository
{
    /// <summary>Persiste una nueva evidencia. Nunca sobrescribe registros existentes (FR-021-01, FR-021-02).</summary>
    Task AddAsync(ModerationAuditRecord record, CancellationToken cancellationToken = default);

    /// <summary>
    /// Consulta paginada, del registro más reciente al más antiguo (FR-021-03, NFR-021-02).
    /// </summary>
    Task<(IReadOnlyList<ModerationAuditRecord> Items, long TotalCount)> ListAsync(
        Guid? targetId,
        ModerationTargetType? targetType,
        ModerationVerdict? verdict,
        DateTime? fromUtc,
        DateTime? toUtc,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
}
