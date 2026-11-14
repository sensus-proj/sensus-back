using Sensus.Domain.Entities;

namespace Sensus.Domain.Repositories;

/// <summary>
/// Contrato de persistencia de las vistas de oferta deduplicadas por usuario y día (spec 032).
/// </summary>
public interface IOfferViewRepository
{
    /// <summary>
    /// Registra la vista si no existe otra del mismo usuario, oferta y día.
    /// Devuelve <c>false</c> cuando el índice único detecta un duplicado (CA-032-04).
    /// </summary>
    Task<bool> TryRegisterAsync(OfferView view, CancellationToken cancellationToken = default);
}
