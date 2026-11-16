using Sensus.Domain.Common;
using Sensus.Domain.Entities;
using Sensus.Domain.Enums;

namespace Sensus.Domain.ValueObjects;

public static class BudgetCalculator
{
    public static Result<RecommendedPackage> BuildPackage(
        decimal budgetMxn,
        IEnumerable<RecommendedPackageItem> candidateItems)
    {
        if (budgetMxn <= 0)
        {
            return Result.Failure<RecommendedPackage>(
                Error.Validation("Ai.InvalidBudget", "El presupuesto debe ser mayor a 0 MXN."));
        }

        var validItems = candidateItems
            .Where(i => i.AvailableStock > 0 && i.IsOpenNow && i.OfferPriceMxn > 0)
            .OrderBy(i => i.OfferPriceMxn)
            .ToList();

        var selected = new List<RecommendedPackageItem>();
        var currentSum = 0m;

        foreach (var item in validItems)
        {
            if (currentSum + item.OfferPriceMxn <= budgetMxn)
            {
                selected.Add(item);
                currentSum += item.OfferPriceMxn;
            }
        }

        if (selected.Count == 0)
        {
            return Result.Failure<RecommendedPackage>(
                Error.Validation("Ai.NoItemsWithinBudget", "No se encontraron ofertas vigentes y disponibles dentro del presupuesto indicado."));
        }

        return RecommendedPackage.Create(budgetMxn, selected);
    }

    public static Result<RecommendedPackage> TryCreatePackage(
        IEnumerable<Offer> offers,
        decimal budgetMxn,
        DateTime nowUtc,
        string? reason = null)
    {
        var items = offers
            .Where(o => o.IsActive(nowUtc))
            .Select(o => new RecommendedPackageItem(
                o.Id.ToString(),
                o.Title,
                o.MerchantId.ToString(),
                string.Empty,
                o.DiscountedPrice,
                o.OriginalPrice,
                o.Stock,
                true))
            .ToList();

        return BuildPackage(budgetMxn, items);
    }

    public static decimal EstimateTurnCostUsd(
        AiRoutingTier tier,
        int promptTokens,
        int cachedTokens,
        int completionTokens)
    {
        decimal promptRate = tier == AiRoutingTier.Fast ? 0.00000015m : 0.0000025m;
        decimal cachedRate = tier == AiRoutingTier.Fast ? 0.000000075m : 0.00000125m;
        decimal completionRate = tier == AiRoutingTier.Fast ? 0.00000060m : 0.0000100m;

        int uncachedPromptTokens = Math.Max(0, promptTokens - cachedTokens);
        return (uncachedPromptTokens * promptRate) +
               (cachedTokens * cachedRate) +
               (completionTokens * completionRate);
    }
}
