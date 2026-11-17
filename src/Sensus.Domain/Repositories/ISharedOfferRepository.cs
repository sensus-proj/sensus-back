using Sensus.Domain.Entities;

namespace Sensus.Domain.Repositories;

/// <summary>
/// Repositorio para ofertas compartidas directamente entre amigos en Sensus.
/// </summary>
public interface ISharedOfferRepository
{
    Task AddRangeAsync(IReadOnlyCollection<SharedOffer> items, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SharedOffer>> GetConversationAsync(Guid userA, Guid userB, CancellationToken cancellationToken = default);
}
