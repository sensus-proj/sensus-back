using Sensus.Domain.Entities;
using Sensus.Domain.Enums;

namespace Sensus.Domain.Repositories;

/// <summary>
/// Repositorio de sanciones (Spec 020). No expone eliminación: el registro es de solo adición (Principio VIII).
/// </summary>
public interface ISanctionRepository
{
    Task<Sanction?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Todas las sanciones del usuario, incluidas las revertidas y vencidas, para copiar su evidencia
    /// de moderación anonimizada al cerrar la cuenta (Spec 061, FR-061-05).
    /// </summary>
    Task<IReadOnlyList<Sanction>> GetAllByUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>Sanción automática ya emitida para un activo, para garantizar idempotencia.</summary>
    Task<Sanction?> GetByMediaIdAsync(Guid mediaId, CancellationToken cancellationToken = default);

    /// <summary>Sanciones activas del usuario en el instante indicado.</summary>
    Task<IReadOnlyList<Sanction>> GetActiveByUserAsync(
        Guid userId,
        DateTime nowUtc,
        CancellationToken cancellationToken = default);

    /// <summary>Cuenta las sanciones no revertidas iniciadas desde la fecha indicada (reincidencia).</summary>
    Task<int> CountNotRevertedSinceAsync(
        Guid userId,
        DateTime sinceUtc,
        CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<Sanction> Items, long TotalCount)> ListAsync(
        Guid? userId,
        SanctionState? state,
        DateTime nowUtc,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task AddAsync(Sanction sanction, CancellationToken cancellationToken = default);

    Task UpdateAsync(Sanction sanction, CancellationToken cancellationToken = default);
}
