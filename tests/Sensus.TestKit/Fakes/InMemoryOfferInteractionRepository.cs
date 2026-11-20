using System.Collections.Concurrent;
using Sensus.Domain.Entities;
using Sensus.Domain.Enums;
using Sensus.Domain.Repositories;

namespace Sensus.TestKit.Fakes;

/// <summary>
/// Repositorio en memoria de interacciones de ofertas para pruebas E2E sin MongoDB.
/// </summary>
public sealed class InMemoryOfferInteractionRepository : IOfferInteractionRepository
{
    private readonly ConcurrentBag<OfferInteraction> _entries = [];

    public IReadOnlyList<OfferInteraction> Snapshot => _entries.ToList();

    public Task InsertAsync(OfferInteraction interaction, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(interaction);
        _entries.Add(interaction);
        return Task.CompletedTask;
    }

    public Task<long> DeleteByPseudonymsAsync(
        IReadOnlyCollection<(string PseudoUserId, int KeyVersion)> pseudonyms,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var set = pseudonyms.ToHashSet();
        var kept = new List<OfferInteraction>();
        long removed = 0;

        while (_entries.TryTake(out OfferInteraction? item))
        {
            if (set.Contains((item.PseudoUserId, item.KeyVersion)))
            {
                removed++;
            }
            else
            {
                kept.Add(item);
            }
        }

        foreach (OfferInteraction keep in kept)
        {
            _entries.Add(keep);
        }

        return Task.FromResult(removed);
    }

    public Task<IReadOnlyList<InteractionQualityAggregate>> AggregateDailyQualityAsync(
        DateTime sinceUtc,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        DateTime cutoff = sinceUtc.ToUniversalTime();

        var grouped = _entries
            .Where(e => e.OccurredAtUtc >= cutoff)
            .SelectMany(e => e.Items.Select(i => (e.Type, e.RankingVersion, i.Slot)))
            .GroupBy(x => (x.RankingVersion, x.Slot))
            .Select(g => new InteractionQualityAggregate(
                g.Key.RankingVersion,
                g.Key.Slot,
                g.Count(x => x.Type == OfferInteractionType.Impression),
                g.Count(x => x.Type == OfferInteractionType.Open),
                g.Count(x => x.Type == OfferInteractionType.Claim)))
            .ToList();

        return Task.FromResult<IReadOnlyList<InteractionQualityAggregate>>(grouped);
    }

    public Task<InteractionCollectionStorageSize> GetStorageSizeAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        long approx = _entries.Count * 512L;
        return Task.FromResult(new InteractionCollectionStorageSize(approx, approx));
    }
}
