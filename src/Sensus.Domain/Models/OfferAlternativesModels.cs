using Sensus.Domain.Enums;
using Sensus.Domain.Policies;

namespace Sensus.Domain.Models;

/// <summary>
/// Candidata de oferta alternativa proyectada desde el repositorio (Spec 043).
/// </summary>
public sealed record OfferAlternativeCandidate(
    Guid OfferId,
    string Title,
    string Category,
    string? ImageUrl,
    decimal OriginalPrice,
    decimal FinalPrice,
    int Stock,
    double DistanceMeters,
    DateTime ExpiresAtUtc,
    Guid MerchantId,
    string MerchantName);

/// <summary>
/// Resultado de consulta de la oferta origen y sus alternativas candidatas en el repositorio (Spec 043).
/// </summary>
public sealed record OfferAlternativesLookupResult(
    bool FoundAndVisible,
    IReadOnlyList<OfferAlternativeCandidate> Candidates)
{
    public static OfferAlternativesLookupResult NotFoundOrUnavailable() => new(false, []);
}

/// <summary>
/// Invariantes y reglas de filtrado y ordenamiento determinista para alternativas de oferta (Spec 043).
/// </summary>
public static class OfferAlternativesPolicy
{
    public const int RegisteredMaxDistanceMeters = 10_000;
    public const int GuestMaxDistanceMeters = GuestRadiusPolicy.GuestMaxRadiusMeters; // 2000 m (FR-043-11)
    public const int DefaultLimit = 5;
    public const int MaxLimit = 10;
    public const decimal DefaultComparablePriceRatio = 1.20m;

    public static int ResolveMaxDistanceMeters(bool isGuest, int registeredMaxDistanceMeters = RegisteredMaxDistanceMeters)
    {
        int cappedRegistered = Math.Clamp(registeredMaxDistanceMeters, 1, 50_000);
        return GuestRadiusPolicy.ResolveRadius(isGuest, isGuest ? Math.Min(GuestMaxDistanceMeters, cappedRegistered) : cappedRegistered);
    }

    public static decimal ComputeMaxComparableFinalPrice(
        decimal originalFinalPrice,
        decimal comparablePriceRatio = DefaultComparablePriceRatio)
    {
        if (originalFinalPrice <= 0m)
        {
            return 0m;
        }

        decimal ratio = comparablePriceRatio > 0m ? comparablePriceRatio : DefaultComparablePriceRatio;
        return decimal.Round(originalFinalPrice * ratio, 2, MidpointRounding.AwayFromZero);
    }

    public static IReadOnlyList<OfferAlternativeCandidate> FilterAndSort(
        Guid originalOfferId,
        OfferCategory originalCategory,
        decimal maxComparableFinalPrice,
        int maxDistanceMeters,
        IReadOnlyList<OfferAlternativeCandidate> candidates,
        int limit)
    {
        ArgumentNullException.ThrowIfNull(candidates);

        int safeLimit = Math.Clamp(limit, 1, MaxLimit);

        return candidates
            .Where(c => c.OfferId != originalOfferId)
            .Where(c => c.Stock > 0)
            .Where(c => c.DistanceMeters <= maxDistanceMeters)
            .Where(c => c.FinalPrice <= maxComparableFinalPrice)
            .Where(c => OfferCategoryCatalog.TryNormalizeLegacy(c.Category, out OfferCategory parsed) && parsed == originalCategory)
            .OrderBy(c => c.DistanceMeters)
            .ThenByDescending(c => c.ExpiresAtUtc)
            .ThenBy(c => c.OfferId)
            .Take(safeLimit)
            .ToList();
    }
}
