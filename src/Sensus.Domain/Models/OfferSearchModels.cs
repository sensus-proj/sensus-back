using System.Text;
using Sensus.Domain.Enums;

namespace Sensus.Domain.Models;

/// <summary>
/// Normalizador determinista de texto en español para indexación y coincidencia por prefijos (Spec 041).
/// Convierte a minúsculas, elimina acentos y colapsa espacios consecutivos.
/// </summary>
public static class OfferTextNormalizer
{
    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        string withoutDiacritics = OfferCategoryCatalog.RemoveDiacritics(value.Trim())
            .ToLowerInvariant();

        var builder = new StringBuilder(withoutDiacritics.Length);
        bool previousSpace = false;
        foreach (char ch in withoutDiacritics)
        {
            if (char.IsWhiteSpace(ch))
            {
                if (!previousSpace)
                {
                    builder.Append(' ');
                    previousSpace = true;
                }
            }
            else
            {
                builder.Append(ch);
                previousSpace = false;
            }
        }

        return builder.ToString().Trim();
    }

    public static string BuildCategorySearchText(string? category)
    {
        if (string.IsNullOrWhiteSpace(category))
        {
            return string.Empty;
        }

        if (OfferCategoryCatalog.TryNormalizeLegacy(category, out OfferCategory canonical))
        {
            string spanish = OfferCategoryCatalog.GetSpanishName(canonical);
            string normalizedSpanish = Normalize(spanish);
            return $"{canonical} {spanish} {normalizedSpanish}";
        }

        return $"{category.Trim()} {Normalize(category)}";
    }

    public static HashSet<OfferCategory> ExtractMatchingCategories(string? normalizedQuery)
    {
        var matched = new HashSet<OfferCategory>();
        if (string.IsNullOrWhiteSpace(normalizedQuery))
        {
            return matched;
        }

        if (OfferCategoryCatalog.TryNormalizeLegacy(normalizedQuery, out OfferCategory direct))
        {
            matched.Add(direct);
        }

        foreach (OfferCategory cat in OfferCategoryCatalog.All)
        {
            string codeNorm = Normalize(cat.ToString());
            string spanishNorm = Normalize(OfferCategoryCatalog.GetSpanishName(cat));
            if (normalizedQuery == codeNorm ||
                normalizedQuery == spanishNorm ||
                (normalizedQuery.Length >= 2 && (codeNorm.StartsWith(normalizedQuery, StringComparison.Ordinal) || spanishNorm.StartsWith(normalizedQuery, StringComparison.Ordinal))))
            {
                matched.Add(cat);
                continue;
            }

            foreach (string token in normalizedQuery.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                if (token.Length < 2)
                {
                    continue;
                }

                if (OfferCategoryCatalog.TryNormalizeLegacy(token, out OfferCategory tokenCat))
                {
                    matched.Add(tokenCat);
                }
                else if (codeNorm.StartsWith(token, StringComparison.Ordinal) || spanishNorm.StartsWith(token, StringComparison.Ordinal))
                {
                    matched.Add(cat);
                }
            }
        }

        return matched;
    }
}

/// <summary>
/// Pesos configurables para el ranking determinista de búsqueda de ofertas (Spec 041, FR-041-02, FR-041-07).
/// </summary>
public sealed record OfferSearchWeights(
    double Text = 0.45d,
    double CategoryMatch = 0.25d,
    double Proximity = 0.20d,
    double Popularity = 0.10d);

/// <summary>
/// Política de evaluación para el ranking de búsqueda de ofertas (Spec 041).
/// </summary>
public sealed record OfferSearchPolicy(
    OfferSearchWeights Weights,
    int RadiusMeters);

/// <summary>
/// Candidata proyectada desde el repositorio para calcular el ranking determinista de búsqueda (Spec 041).
/// </summary>
public sealed record OfferSearchCandidate(
    Guid OfferId,
    string Title,
    string Description,
    string Category,
    string? ImageUrl,
    decimal OriginalPrice,
    decimal FinalPrice,
    int Stock,
    double DistanceMeters,
    DateTime ExpiresAtUtc,
    Guid MerchantId,
    string MerchantName,
    int ViewCount,
    double MongoTextScore = 0d);

/// <summary>
/// Resultado individual ya puntuado por <see cref="Services.OfferSearchRanker"/>.
/// </summary>
public sealed record RankedOfferSearchEntry(
    OfferSearchCandidate Candidate,
    double Score);
