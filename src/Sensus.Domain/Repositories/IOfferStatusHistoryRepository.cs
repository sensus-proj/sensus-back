using Sensus.Domain.Entities;

namespace Sensus.Domain.Repositories;

/// <summary>
/// Repositorio para persistir el historial de cambios de estado de ofertas (Spec 036).
/// </summary>
public interface IOfferStatusHistoryRepository
{
    Task AddAsync(OfferStatusTransition transition, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<OfferStatusTransition>> GetByOfferIdAsync(
        Guid offerId,
        CancellationToken cancellationToken = default);
}
