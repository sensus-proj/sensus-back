using Sensus.Domain.Enums;
using Sensus.Domain.Models;

namespace Sensus.Application.Offers.DTOs;

/// <summary>
/// Elemento individual de respuesta en la búsqueda de ofertas (Spec 041).
/// </summary>
public sealed record OfferSearchItemDto(
    Guid OfferId,
    string Title,
    string Description,
    string? Category,
    string? ImageUrl,
    decimal OriginalPrice,
    decimal FinalPrice,
    int Stock,
    int DistanceMeters,
    DateTime ExpiresAtUtc,
    Guid MerchantId,
    string MerchantName,
    double Score);

/// <summary>
/// Elemento individual de alternativa a una oferta (Spec 043).
/// </summary>
public sealed record OfferAlternativeItemDto(
    Guid OfferId,
    string Title,
    string Category,
    string? ImageUrl,
    decimal OriginalPrice,
    decimal FinalPrice,
    int Stock,
    int DistanceMeters,
    DateTime ExpiresAtUtc,
    Guid MerchantId,
    string MerchantName);

/// <summary>
/// Mapeo explícito entre modelos de dominio de búsqueda/alternativas y DTOs de contrato (AGENTS.md §7.3).
/// </summary>
public static class OfferDiscoveryMappingExtensions
{
    public static OfferSearchItemDto ToDto(this RankedOfferSearchEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        OfferSearchCandidate c = entry.Candidate;

        string? normalizedCategory = string.IsNullOrWhiteSpace(c.Category)
            ? null
            : OfferCategoryCatalog.TryNormalizeLegacy(c.Category, out OfferCategory parsed)
                ? parsed.ToString()
                : c.Category;

        return new OfferSearchItemDto(
            c.OfferId,
            c.Title,
            c.Description,
            normalizedCategory,
            c.ImageUrl,
            c.OriginalPrice,
            c.FinalPrice,
            c.Stock,
            Math.Max(0, (int)Math.Round(c.DistanceMeters, MidpointRounding.AwayFromZero)),
            c.ExpiresAtUtc,
            c.MerchantId,
            c.MerchantName,
            entry.Score);
    }

    public static OfferAlternativeItemDto ToDto(this OfferAlternativeCandidate candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        string canonicalCategory = OfferCategoryCatalog.TryNormalizeLegacy(candidate.Category, out OfferCategory parsed)
            ? parsed.ToString()
            : candidate.Category;

        return new OfferAlternativeItemDto(
            candidate.OfferId,
            candidate.Title,
            canonicalCategory,
            candidate.ImageUrl,
            candidate.OriginalPrice,
            candidate.FinalPrice,
            candidate.Stock,
            Math.Max(0, (int)Math.Round(candidate.DistanceMeters, MidpointRounding.AwayFromZero)),
            candidate.ExpiresAtUtc,
            candidate.MerchantId,
            candidate.MerchantName);
    }
}
