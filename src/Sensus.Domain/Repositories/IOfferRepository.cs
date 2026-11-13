using Sensus.Domain.Entities;

namespace Sensus.Domain.Repositories;

/// <summary>
/// Contrato de repositorio para la entidad de dominio Offer con soporte para MongoDB Atlas y control de expiración masiva (SE-022 a SE-026).
/// </summary>
public interface IOfferRepository
{
    Task<Offer?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Offer>> GetByMerchantIdAsync(Guid merchantId, CancellationToken cancellationToken = default);
    Task AddAsync(Offer offer, CancellationToken cancellationToken = default);
    Task UpdateAsync(Offer offer, CancellationToken cancellationToken = default);

    /// <summary>
    /// Transición masiva y atómica en MongoDB para expirar ofertas cuya fecha de fin ya transcurrió (Sustituto de Trigger/SQL Job).
    /// </summary>
    Task<int> ExpireOffersAsync(DateTime nowUtc, CancellationToken cancellationToken = default);
}
