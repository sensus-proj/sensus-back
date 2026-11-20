using Sensus.Domain.Entities;
using Sensus.Domain.Enums;

namespace Sensus.Domain.Repositories;

/// <summary>
/// Lectura agregada diaria para métricas de calidad de interacciones por versión de ranking y slot (FR-055-10).
/// </summary>
public sealed record InteractionQualityAggregate(
    string RankingVersion,
    FeedSlot Slot,
    long Impressions,
    long Opens,
    long Claims);

/// <summary>
/// Lectura cruda de tamaño de colección y clúster obtenida desde <c>collStats</c> y <c>dbStats</c> (NFR-055-04).
/// </summary>
public sealed record InteractionCollectionStorageSize(
    long CollectionBytes,
    long ClusterDataBytes);

/// <summary>
/// Persistencia de interacciones seudonimizadas sobre <c>offer_interactions</c> (Spec 055).
/// </summary>
public interface IOfferInteractionRepository
{
    Task InsertAsync(OfferInteraction interaction, CancellationToken cancellationToken = default);

    /// <summary>Elimina las interacciones asociadas a los pares (seudoId, versión) del usuario (FR-055-07).</summary>
    Task<long> DeleteByPseudonymsAsync(
        IReadOnlyCollection<(string PseudoUserId, int KeyVersion)> pseudonyms,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<InteractionQualityAggregate>> AggregateDailyQualityAsync(
        DateTime sinceUtc,
        CancellationToken cancellationToken = default);

    Task<InteractionCollectionStorageSize> GetStorageSizeAsync(CancellationToken cancellationToken = default);
}
