using System.Collections.Concurrent;
using Sensus.Domain.Entities;
using Sensus.Domain.Repositories;

namespace Sensus.TestKit.Fakes;

public sealed class InMemorySharedOfferRepository : ISharedOfferRepository
{
    private readonly ConcurrentQueue<SharedOffer> _items = new();

    public Task AddRangeAsync(IReadOnlyCollection<SharedOffer> items, CancellationToken cancellationToken = default)
    {
        foreach (SharedOffer item in items)
        {
            _items.Enqueue(item);
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<SharedOffer>> GetConversationAsync(
        Guid userA,
        Guid userB,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<SharedOffer> result = _items
            .Where(s => (s.SenderUserId == userA && s.RecipientUserId == userB) ||
                        (s.SenderUserId == userB && s.RecipientUserId == userA))
            .OrderBy(s => s.SharedAtUtc)
            .ToList();

        return Task.FromResult(result);
    }
}
