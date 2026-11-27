using Sensus.Domain.Entities;
using Sensus.Domain.Enums;

namespace Sensus.Domain.Models;

/// <summary>
/// Proyección de lectura de una oferta favorita vigente con datos del comercio y fecha de guardado (FR-038-07).
/// </summary>
public sealed record FavoriteOfferEntry(
    Offer Offer,
    string MerchantName,
    MerchantCategory MerchantCategory,
    DateTime FavoritedAtUtc);

/// <summary>
/// Página de ofertas favoritas visibles de un usuario (FR-038-07).
/// </summary>
public sealed record FavoriteOffersPage(
    IReadOnlyList<FavoriteOfferEntry> Items,
    int TotalCount);
