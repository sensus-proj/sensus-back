using Sensus.Domain.Entities;
using Sensus.Domain.Enums;

namespace Sensus.Application.Common.Interfaces;

/// <summary>
/// Puerto de mejor esfuerzo para registrar interacciones seudonimizadas sin bloquear las operaciones principales (Spec 055).
/// </summary>
public interface IOfferInteractionRecorder
{
    Task RecordImpressionsAsync(
        Guid userId,
        string? snapshotId,
        string rankingVersion,
        int page,
        IReadOnlyList<OfferInteractionItem> items,
        CancellationToken cancellationToken = default);

    Task RecordSingleAsync(
        Guid userId,
        OfferInteractionType type,
        OfferInteractionItem item,
        string rankingVersion = "direct",
        string? snapshotId = null,
        CancellationToken cancellationToken = default);

    /// <summary>Purga inmediata de todos los eventos del usuario usando todas las versiones de clave HMAC (FR-055-07).</summary>
    Task PurgeByUserAsync(Guid userId, CancellationToken cancellationToken = default);
}
