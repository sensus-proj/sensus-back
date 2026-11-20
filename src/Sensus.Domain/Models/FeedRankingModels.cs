using Sensus.Domain.Common;
using Sensus.Domain.Enums;

namespace Sensus.Domain.Models;

/// <summary>
/// Pesos configurables y versionados de la fórmula determinista de ranking (Spec 042, FR-042-02, NFR-042-07).
/// </summary>
public sealed record FeedRankingWeights(
    double Interest,
    double Affinity,
    double Proximity,
    double Urgency,
    double Popularity)
{
    public static readonly FeedRankingWeights Default = new(
        Interest: 0.40d,
        Affinity: 0.25d,
        Proximity: 0.20d,
        Urgency: 0.10d,
        Popularity: 0.05d);

    public Result Validate()
    {
        double[] values = [Interest, Affinity, Proximity, Urgency, Popularity];
        if (values.Any(v => double.IsNaN(v) || double.IsInfinity(v) || v < 0d))
        {
            return Result.Failure(
                Error.Validation("FeedRanking.InvalidWeight", "Todos los pesos del ranking deben ser finitos y mayores o iguales a 0."));
        }

        if (values.Sum() <= 0d)
        {
            return Result.Failure(
                Error.Validation("FeedRanking.ZeroWeights", "La suma de los pesos del ranking debe ser mayor que 0."));
        }

        return Result.Success();
    }
}

/// <summary>
/// Política completa de ordenamiento, diversidad y exploración del feed (Spec 042).
/// </summary>
public sealed record FeedRankingPolicy(
    string Version,
    FeedRankingWeights Weights,
    int MaxConsecutiveSameCategory = 3,
    int ExplorationEvery = 5,
    int NewOfferHours = 48,
    int MaxSnapshotOffers = 500)
{
    public Result Validate()
    {
        if (string.IsNullOrWhiteSpace(Version) || Version.Trim().Length > 32)
        {
            return Result.Failure(
                Error.Validation("FeedRanking.InvalidVersion", "La versión de ranking es obligatoria y acotada."));
        }

        Result weightCheck = Weights.Validate();
        if (weightCheck.IsFailure)
        {
            return weightCheck;
        }

        if (MaxConsecutiveSameCategory < 1 || MaxConsecutiveSameCategory > 20)
        {
            return Result.Failure(
                Error.Validation("FeedRanking.InvalidDiversity", "El límite consecutivo por categoría debe estar entre 1 y 20."));
        }

        if (ExplorationEvery < 0 || ExplorationEvery > 100)
        {
            return Result.Failure(
                Error.Validation("FeedRanking.InvalidExploration", "El intervalo de exploración debe estar entre 0 y 100."));
        }

        if (NewOfferHours < 1 || NewOfferHours > 720)
        {
            return Result.Failure(
                Error.Validation("FeedRanking.InvalidNewOfferHours", "La ventana de novedad para exploración debe estar entre 1 y 720 horas."));
        }

        if (MaxSnapshotOffers < 1 || MaxSnapshotOffers > 500)
        {
            return Result.Failure(
                Error.Validation("FeedRanking.InvalidSnapshotSize", "El tamaño de instantánea debe estar entre 1 y 500."));
        }

        return Result.Success();
    }
}

/// <summary>
/// Oferta candidata elegible devuelta desde el radio geográfico del usuario (FR-042-01, FR-042-05).
/// </summary>
public sealed record FeedOfferCandidate(
    Guid OfferId,
    string Title,
    string Category,
    string? ImageUrl,
    decimal OriginalPrice,
    decimal FinalPrice,
    int Stock,
    double DistanceMeters,
    DateTime CreatedAtUtc,
    DateTime ExpiresAtUtc,
    Guid MerchantId,
    string MerchantName,
    int EngagementCount);

/// <summary>
/// Elemento ya ordenado en la lista del ranking con su origen (<see cref="FeedSlot"/>).
/// </summary>
public sealed record RankedFeedEntry(
    FeedOfferCandidate Candidate,
    double Score,
    FeedSlot Slot);

/// <summary>
/// Resultado completo de <see cref="Services.OfferFeedRanker"/> incluyendo señales de degradación/relajación.
/// </summary>
public sealed record FeedRankResult(
    IReadOnlyList<RankedFeedEntry> Entries,
    bool RelaxedDiversity,
    bool RelaxedExploration,
    bool UsedFallback);
