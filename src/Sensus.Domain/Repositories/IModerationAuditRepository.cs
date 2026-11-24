using Sensus.Domain.Entities;
using Sensus.Domain.Enums;

namespace Sensus.Domain.Repositories;

/// <summary>
/// Repositorio de evidencia de moderación (Spec 021) y auditoría anonimizada tras baja de cuenta (Spec 061).
/// Deliberadamente no expone actualización ni borrado: el registro es de solo adición (NFR-021-01, FR-061-05).
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

    /// <summary>
    /// Inserta la entrada anonimizada si no existe otra con el mismo identificador determinista.
    /// Devuelve <c>true</c> solo cuando se insertó una entrada nueva (idempotencia, FR-061-05, FR-061-06).
    /// </summary>
    Task<bool> AddIfNotExistsAsync(
        ModerationAudit entry,
        CancellationToken cancellationToken = default);
}