using System.Security.Cryptography;
using System.Text;
using Sensus.Domain.Entities;
using Sensus.Domain.Enums;
using Sensus.Domain.Models;

namespace Sensus.Domain.Services;

/// <summary>
/// Función pura y determinista que ordena ofertas cercanas aplicando interés explícito, afinidad inferida,
/// cercanía, urgencia, popularidad por percentil, diversidad de categorías y cuota de exploración
/// (Spec 042, ADR 0009, NFR-042-06).
/// </summary>
public static class OfferFeedRanker
{
    private const double MinUrgencyHours = 24d;
    private const double MaxUrgencyHours = 168d; // 7 días

    public static FeedRankResult Rank(
        Guid userId,
        IReadOnlyList<FeedOfferCandidate> candidates,
        IReadOnlyCollection<OfferCategory> userInterests,
        IReadOnlyDictionary<string, double> categoryAffinities,
        int radiusKm,
        DateTime nowUtc,
        FeedRankingPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(userInterests);
        ArgumentNullException.ThrowIfNull(categoryAffinities);
        ArgumentNullException.ThrowIfNull(policy);

        if (candidates.Count == 0)
        {
            return new FeedRankResult([], false, false, false);
        }

        DateTime utcNow = nowUtc.ToUniversalTime();
        double radiusMeters = Math.Max(radiusKm, 1) * 1000d;

        HashSet<string> interestNames = userInterests
            .Where(Enum.IsDefined)
            .Select(c => c.ToString())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        bool hasExplicitInterests = interestNames.Count > 0;
        bool hasAffinitySignals = categoryAffinities.Values.Any(v => v > 0d);
        bool useFallback = !hasExplicitInterests && !hasAffinitySignals;

        Dictionary<Guid, double> popularityPercentiles = ComputePopularityPercentiles(candidates);

        var scored = new List<ScoredCandidate>(candidates.Count);
        foreach (FeedOfferCandidate candidate in candidates)
        {
            string canonicalCategory = NormalizeCategory(candidate.Category);
            FeedOfferCandidate normalizedCandidate = candidate with { Category = canonicalCategory };

            double proximity = Math.Clamp(1d - (candidate.DistanceMeters / radiusMeters), 0d, 1d);
            double urgency = ComputeUrgency(candidate.ExpiresAtUtc, utcNow);

            double score;
            if (useFallback)
            {
                // FR-042-09 / CA-042-04: sin intereses ni señales, fallback por cercanía y urgencia.
                double pw = policy.Weights.Proximity;
                double uw = policy.Weights.Urgency;
                double sum = pw + uw;
                score = sum > 0d
                    ? ((pw * proximity) + (uw * urgency)) / sum
                    : (0.7d * proximity) + (0.3d * urgency);
            }
            else
            {
                double interestSignal = interestNames.Contains(canonicalCategory) ? 1d : 0d;
                double affinitySignal = categoryAffinities.TryGetValue(canonicalCategory, out double aff)
                    ? Math.Clamp(aff, 0d, 1d)
                    : 0d;
                double popularitySignal = popularityPercentiles.GetValueOrDefault(candidate.OfferId, 0d);

                score =
                    (policy.Weights.Interest * interestSignal) +
                    (policy.Weights.Affinity * affinitySignal) +
                    (policy.Weights.Proximity * proximity) +
                    (policy.Weights.Urgency * urgency) +
                    (policy.Weights.Popularity * popularitySignal);
            }

            bool isNewOffer = (utcNow - candidate.CreatedAtUtc.ToUniversalTime()).TotalHours <= policy.NewOfferHours;
            bool isOutsideInterests = !hasExplicitInterests || !interestNames.Contains(canonicalCategory);
            bool isExplorationEligible = isNewOffer || (hasExplicitInterests && isOutsideInterests);
            ulong explorationTieKey = ComputeStableExplorationKey(userId, utcNow, candidate.OfferId);

            scored.Add(new ScoredCandidate(
                normalizedCandidate,
                Math.Round(score, 10),
                isExplorationEligible,
                explorationTieKey));
        }

        // Orden base determinista: puntaje desc -> fecha de expiración asc -> OfferId asc (plan §1).
        // En fallback sin intereses ni señales, el desempate natural adicional favorece menor distancia primero.
        var baseQueue = scored
            .OrderByDescending(s => s.Score)
            .ThenBy(s => useFallback ? s.Candidate.DistanceMeters : 0d)
            .ThenBy(s => s.Candidate.ExpiresAtUtc)
            .ThenBy(s => s.Candidate.OfferId)
            .ToList();

        var explorationQueue = scored
            .Where(s => s.IsExplorationEligible)
            .OrderBy(s => s.ExplorationTieKey)
            .ThenBy(s => s.Candidate.OfferId)
            .ToList();

        int targetCount = Math.Min(policy.MaxSnapshotOffers, scored.Count);
        var usedIds = new HashSet<Guid>(targetCount);
        var result = new List<RankedFeedEntry>(targetCount);

        bool relaxedDiversity = false;
        bool relaxedExploration = false;

        for (int pos = 1; pos <= targetCount; pos++)
        {
            bool isExplorationPosition = !useFallback &&
                policy.ExplorationEvery > 0 &&
                (pos % policy.ExplorationEvery == 0);

            if (isExplorationPosition)
            {
                ScoredCandidate? exp = PickNextRespectingDiversity(
                    explorationQueue,
                    usedIds,
                    result,
                    policy.MaxConsecutiveSameCategory,
                    allowDiversityRelaxation: true,
                    out bool expRelaxed);

                if (exp is not null)
                {
                    if (expRelaxed)
                    {
                        relaxedDiversity = true;
                    }

                    usedIds.Add(exp.Candidate.OfferId);
                    result.Add(new RankedFeedEntry(exp.Candidate, exp.Score, FeedSlot.Exploration));
                    continue;
                }

                relaxedExploration = true;
            }

            ScoredCandidate? ranked = PickNextRespectingDiversity(
                baseQueue,
                usedIds,
                result,
                policy.MaxConsecutiveSameCategory,
                allowDiversityRelaxation: true,
                out bool divRelaxed);

            if (ranked is null)
            {
                break;
            }

            if (divRelaxed)
            {
                relaxedDiversity = true;
            }

            usedIds.Add(ranked.Candidate.OfferId);
            result.Add(new RankedFeedEntry(ranked.Candidate, ranked.Score, FeedSlot.Ranked));
        }

        return new FeedRankResult(result, relaxedDiversity, relaxedExploration, useFallback);
    }

    private static ScoredCandidate? PickNextRespectingDiversity(
        List<ScoredCandidate> orderedQueue,
        HashSet<Guid> usedIds,
        List<RankedFeedEntry> currentOutput,
        int maxConsecutive,
        bool allowDiversityRelaxation,
        out bool relaxedDiversity)
    {
        relaxedDiversity = false;
        ScoredCandidate? firstAvailable = null;

        string? blockedCategory = GetTrailingStreakCategory(currentOutput, maxConsecutive);

        foreach (ScoredCandidate candidate in orderedQueue)
        {
            if (usedIds.Contains(candidate.Candidate.OfferId))
            {
                continue;
            }

            firstAvailable ??= candidate;

            if (blockedCategory is null ||
                !string.Equals(candidate.Candidate.Category, blockedCategory, StringComparison.OrdinalIgnoreCase))
            {
                return candidate;
            }
        }

        if (allowDiversityRelaxation && firstAvailable is not null)
        {
            relaxedDiversity = true;
            return firstAvailable;
        }

        return null;
    }

    private static string? GetTrailingStreakCategory(List<RankedFeedEntry> output, int maxConsecutive)
    {
        if (output.Count < maxConsecutive)
        {
            return null;
        }

        string tailCategory = output[^1].Candidate.Category;
        for (int i = 2; i <= maxConsecutive; i++)
        {
            if (!string.Equals(output[^i].Candidate.Category, tailCategory, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }
        }

        return tailCategory;
    }

    private static double ComputeUrgency(DateTime expiresAtUtc, DateTime nowUtc)
    {
        double remainingHours = (expiresAtUtc.ToUniversalTime() - nowUtc).TotalHours;
        if (remainingHours <= MinUrgencyHours)
        {
            return 1d;
        }

        if (remainingHours >= MaxUrgencyHours)
        {
            return 0d;
        }

        return Math.Clamp(1d - ((remainingHours - MinUrgencyHours) / (MaxUrgencyHours - MinUrgencyHours)), 0d, 1d);
    }

    private static Dictionary<Guid, double> ComputePopularityPercentiles(IReadOnlyList<FeedOfferCandidate> candidates) =>
        RankingMath.ComputePopularityPercentiles(candidates, c => c.OfferId, c => c.EngagementCount);

    private static string NormalizeCategory(string? rawCategory)
    {
        if (string.IsNullOrWhiteSpace(rawCategory))
        {
            return OfferInteractionItem.UncategorizedCategory;
        }

        return OfferCategoryCatalog.TryNormalizeLegacy(rawCategory, out OfferCategory parsed)
            ? parsed.ToString()
            : rawCategory.Trim();
    }

    private static ulong ComputeStableExplorationKey(Guid userId, DateTime utcNow, Guid offerId)
    {
        string input = $"{userId:N}:{utcNow:yyyy-MM-dd}:{offerId:N}";
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return BitConverter.ToUInt64(hash, 0);
    }

    private sealed record ScoredCandidate(
        FeedOfferCandidate Candidate,
        double Score,
        bool IsExplorationEligible,
        ulong ExplorationTieKey);
}
