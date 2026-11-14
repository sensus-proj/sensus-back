using Sensus.Domain.Entities;
using Sensus.Domain.Enums;

namespace Sensus.Domain.Models;

/// <summary>
/// Proyección de lectura de una oferta cercana con su distancia y los datos públicos del comercio.
/// </summary>
public sealed record NearbyOfferHit(
    Offer Offer,
    double DistanceMeters,
    string MerchantName,
    MerchantCategory MerchantCategory);

/// <summary>
/// Página de resultados de la consulta geoespacial de ofertas.
/// </summary>
public sealed record NearbyOffersPage(
    IReadOnlyList<NearbyOfferHit> Items,
    int TotalCount);
