using Sensus.Domain.Enums;
using Sensus.Domain.Models;

namespace Sensus.Domain.Services;

/// <summary>
/// Función pura y determinista de ranking para búsqueda híbrida de ofertas:
/// texto (prefijo normalizado + stemming español de MongoDB), coincidencia exacta de categoría,
/// cercanía geoespacial y popularidad (Spec 041, FR-041-02, FR-041-05, NFR-041-02, NFR-041-03).
/// </summary>
public static class OfferSearchRanker
{
    public static IReadOnlyList<RankedOfferSearchEntry> Rank(
        string? rawQuery,
        IReadOnlyList<OfferSearchCandidate> candidates,
        OfferSearchPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(policy);

        if (candidates.Count == 0)
        {
            return [];
        }

        string normalizedQuery = OfferTextNormalizer.Normalize(rawQuery);
        double radiusMeters = Math.Max(1, policy.RadiusMeters);
        Dictionary<Guid, double> popularityPercentiles = RankingMath.ComputePopularityPercentiles(
            candidates,
            c => c.OfferId,
            c => c.ViewCount);

        if (string.IsNullOrEmpty(normalizedQuery))
        {
            // FR-041-05 / CA-041-02: sin texto, orden determinista por cercanía y popularidad.
            return candidates
                .Select(c =>
                {
                    double proximity = Math.Clamp(1d - (c.DistanceMeters / radiusMeters), 0d, 1d);
                    double popularity = popularityPercentiles.GetValueOrDefault(c.OfferId, 0d);
                    double fallbackScore = Math.Round((0.80d * proximity) + (0.20d * popularity), 10);
                    return new RankedOfferSearchEntry(c, fallbackScore);
                })
                .OrderBy(e => e.Candidate.DistanceMeters)
                .ThenByDescending(e => e.Score)
                .ThenBy(e => e.Candidate.OfferId)
                .ToList();
        }

        double maxMongoTextScore = candidates.Max(c => Math.Max(0d, c.MongoTextScore));
        HashSet<OfferCategory> queryCategories = OfferTextNormalizer.ExtractMatchingCategories(normalizedQuery);
        string[] queryTokens = normalizedQuery.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        var ranked = new List<RankedOfferSearchEntry>(candidates.Count);
        foreach (OfferSearchCandidate candidate in candidates)
        {
            double textSignal = ComputeTextSignal(candidate, normalizedQuery, queryTokens, maxMongoTextScore);
            double categorySignal = ComputeCategorySignal(candidate.Category, queryCategories);
            double proximitySignal = Math.Clamp(1d - (candidate.DistanceMeters / radiusMeters), 0d, 1d);
            double popularitySignal = popularityPercentiles.GetValueOrDefault(candidate.OfferId, 0d);

            // Si no hubo coincidencia textual, ni de categoría ni puntaje $text de Mongo, descartar ruido.
            if (textSignal <= 0d && categorySignal <= 0d)
            {
                continue;
            }

            double score =
                (policy.Weights.Text * textSignal) +
                (policy.Weights.CategoryMatch * categorySignal) +
                (policy.Weights.Proximity * proximitySignal) +
                (policy.Weights.Popularity * popularitySignal);

            ranked.Add(new RankedOfferSearchEntry(candidate, Math.Round(score, 10)));
        }

        return ranked
            .OrderByDescending(e => e.Score)
            .ThenBy(e => e.Candidate.DistanceMeters)
            .ThenBy(e => e.Candidate.OfferId)
            .ToList();
    }

    private static double ComputeTextSignal(
        OfferSearchCandidate candidate,
        string normalizedQuery,
        string[] queryTokens,
        double maxMongoTextScore)
    {
        string titleNorm = OfferTextNormalizer.Normalize(candidate.Title);
        string merchantNorm = OfferTextNormalizer.Normalize(candidate.MerchantName);
        string descNorm = OfferTextNormalizer.Normalize(candidate.Description);

        double lexicalScore = 0d;
        if (titleNorm == normalizedQuery)
        {
            lexicalScore = 1.0d;
        }
        else if (titleNorm.StartsWith(normalizedQuery, StringComparison.Ordinal))
        {
            lexicalScore = 0.95d;
        }
        else if (HasTokenPrefixMatch(titleNorm, queryTokens))
        {
            lexicalScore = 0.85d;
        }
        else if (titleNorm.Contains(normalizedQuery, StringComparison.Ordinal))
        {
            lexicalScore = 0.72d;
        }
        else if (merchantNorm == normalizedQuery || merchantNorm.StartsWith(normalizedQuery, StringComparison.Ordinal))
        {
            lexicalScore = 0.70d;
        }
        else if (HasTokenPrefixMatch(merchantNorm, queryTokens) || descNorm.Contains(normalizedQuery, StringComparison.Ordinal))
        {
            lexicalScore = 0.55d;
        }
        else if (HasTokenPrefixMatch(descNorm, queryTokens))
        {
            lexicalScore = 0.40d;
        }

        double mongoNormalized = maxMongoTextScore > 0d && candidate.MongoTextScore > 0d
            ? Math.Clamp(candidate.MongoTextScore / maxMongoTextScore, 0d, 1d)
            : 0d;

        if (lexicalScore == 0d && mongoNormalized == 0d)
        {
            return 0d;
        }

        // Combina señal léxica/prefijo con el puntaje nativo $text (stemming español) de MongoDB.
        return Math.Clamp(Math.Max(lexicalScore, (0.55d * mongoNormalized) + (0.45d * lexicalScore)), 0d, 1d);
    }

    private static bool HasTokenPrefixMatch(string normalizedField, string[] queryTokens)
    {
        if (string.IsNullOrEmpty(normalizedField) || queryTokens.Length == 0)
        {
            return false;
        }

        string[] fieldTokens = normalizedField.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        foreach (string qToken in queryTokens)
        {
            if (qToken.Length < 2)
            {
                continue;
            }

            if (fieldTokens.Any(ft => ft.StartsWith(qToken, StringComparison.Ordinal)))
            {
                return true;
            }
        }

        return false;
    }

    private static double ComputeCategorySignal(string? rawCategory, HashSet<OfferCategory> queryCategories)
    {
        if (queryCategories.Count == 0 || string.IsNullOrWhiteSpace(rawCategory))
        {
            return 0d;
        }

        return OfferCategoryCatalog.TryNormalizeLegacy(rawCategory, out OfferCategory parsed) &&
               queryCategories.Contains(parsed)
            ? 1.0d
            : 0d;
    }
}
