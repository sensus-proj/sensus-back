using Sensus.Domain.Common;

namespace Sensus.Domain.ValueObjects;

public sealed record RecommendedPackageItem(
    string OfferId,
    string Title,
    string MerchantId,
    string MerchantName,
    decimal OfferPriceMxn,
    decimal RegularPriceMxn,
    int AvailableStock,
    bool IsOpenNow,
    double? DistanceKm = null);

public sealed class RecommendedPackage : ValueObject
{
    public decimal BudgetMxn { get; }
    public decimal TotalPriceMxn { get; }
    public decimal SavingsMxn { get; }
    public IReadOnlyList<RecommendedPackageItem> Items { get; }
    public int ItemCount => Items.Count;

    private RecommendedPackage(
        decimal budgetMxn,
        decimal totalPriceMxn,
        decimal savingsMxn,
        IReadOnlyList<RecommendedPackageItem> items)
    {
        BudgetMxn = budgetMxn;
        TotalPriceMxn = totalPriceMxn;
        SavingsMxn = savingsMxn;
        Items = items;
    }

    public static Result<RecommendedPackage> Create(decimal budgetMxn, IEnumerable<RecommendedPackageItem>? items)
    {
        if (budgetMxn <= 0)
        {
            return Result.Failure<RecommendedPackage>(
                Error.Validation("Ai.InvalidBudget", "El presupuesto debe ser mayor a 0 MXN."));
        }

        var itemList = (items ?? []).ToList();
        if (itemList.Count == 0)
        {
            return Result.Failure<RecommendedPackage>(
                Error.Validation("Ai.EmptyPackage", "El paquete recomendado no puede estar vacío."));
        }

        var seenOfferIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in itemList)
        {
            if (string.IsNullOrWhiteSpace(item.OfferId))
            {
                return Result.Failure<RecommendedPackage>(
                    Error.Validation("Ai.InvalidItemOfferId", "Cada ítem del paquete debe tener un identificador de oferta válido."));
            }

            if (!seenOfferIds.Add(item.OfferId.Trim()))
            {
                return Result.Failure<RecommendedPackage>(
                    Error.Validation("Ai.DuplicateOfferInPackage", $"El paquete no puede contener ofertas duplicadas (OfferId: '{item.OfferId}')."));
            }

            if (item.AvailableStock <= 0)
            {
                return Result.Failure<RecommendedPackage>(
                    Error.Validation("Ai.ItemOutOfStock", $"La oferta '{item.Title}' no tiene existencias disponibles."));
            }

            if (!item.IsOpenNow)
            {
                return Result.Failure<RecommendedPackage>(
                    Error.Validation("Ai.MerchantClosed", $"El comercio de la oferta '{item.Title}' se encuentra cerrado actualmente."));
            }

            if (item.OfferPriceMxn <= 0)
            {
                return Result.Failure<RecommendedPackage>(
                    Error.Validation("Ai.InvalidItemPrice", $"El precio de la oferta '{item.Title}' debe ser mayor a 0 MXN."));
            }
        }

        var total = itemList.Sum(i => i.OfferPriceMxn);
        if (total > budgetMxn)
        {
            return Result.Failure<RecommendedPackage>(
                Error.Validation("Ai.BudgetExceeded", $"La suma del paquete (${total} MXN) excede el presupuesto indicado (${budgetMxn} MXN)."));
        }

        var totalRegular = itemList.Sum(i => i.RegularPriceMxn);
        var savings = totalRegular > total ? totalRegular - total : 0m;

        return Result.Success(new RecommendedPackage(budgetMxn, total, savings, itemList.AsReadOnly()));
    }

    public override IEnumerable<object?> GetEqualityComponents()
    {
        yield return BudgetMxn;
        yield return TotalPriceMxn;
        yield return SavingsMxn;
        foreach (var item in Items)
        {
            yield return item.OfferId;
            yield return item.OfferPriceMxn;
        }
    }
}
