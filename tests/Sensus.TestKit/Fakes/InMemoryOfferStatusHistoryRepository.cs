using Sensus.Domain.Entities;
using Sensus.Domain.Repositories;

namespace Sensus.TestKit.Fakes;

public sealed class InMemoryOfferStatusHistoryRepository : IOfferStatusHistoryRepository
{
    private readonly List<OfferStatusTransition> _transitions = [];

    public IReadOnlyList<OfferStatusTransition> All => _transitions;

    public Task AddAsync(OfferStatusTransition transition, CancellationToken cancellationToken = default)
    {
        _transitions.Add(transition);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<OfferStatusTransition>> GetByOfferIdAsync(
        Guid offerId,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<OfferStatusTransition> result = _transitions
            .Where(t => t.OfferId == offerId)
            .OrderBy(t => t.OccurredAtUtc)
            .ToList();
        return Task.FromResult(result);
    }
}
