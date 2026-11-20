using Sensus.Domain.Enums;

namespace Sensus.Application.Common.Interfaces;

/// <summary>
/// Entrada mínima almacenada en la instantánea de Redis del feed (plan §3, máximo 500 por consulta).
/// </summary>
public sealed record FeedSnapshotEntry(Guid OfferId, FeedSlot Slot);

/// <summary>
/// Instantánea ordenada de ofertas por usuario para paginación estable dentro del TTL (FR-042-08).
/// </summary>
public sealed record FeedSnapshot(
    Guid SnapshotId,
    string RankingVersion,
    DateTime CreatedAtUtc,
    IReadOnlyList<FeedSnapshotEntry> Entries);

/// <summary>
/// Almacén de instantáneas por usuario en Redis con degradación transparente ante indisponibilidad (Spec 042).
/// </summary>
public interface IFeedSnapshotStore
{
    Task<FeedSnapshot?> GetAsync(Guid userId, Guid snapshotId, CancellationToken cancellationToken = default);

    /// <summary>Intenta guardar la instantánea; devuelve <c>false</c> si Redis no está disponible (snapshotId será nulo).</summary>
    Task<bool> SaveAsync(Guid userId, FeedSnapshot snapshot, TimeSpan ttl, CancellationToken cancellationToken = default);
}
