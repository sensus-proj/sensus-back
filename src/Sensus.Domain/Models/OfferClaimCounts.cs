namespace Sensus.Domain.Models;

/// <summary>
/// Métricas agregadas de apartados por oferta para el inventario del comerciante (Spec 037, FR-037-03).
/// Solo contiene totales: nunca expone usuarios, códigos ni hashes.
/// </summary>
public sealed record OfferClaimCounts(int ActiveClaims, int Redemptions)
{
    /// <summary>Conteo vacío usado cuando una oferta no tiene apartados.</summary>
    public static readonly OfferClaimCounts Empty = new(0, 0);
}
