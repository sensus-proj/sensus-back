using FluentAssertions;
using Sensus.Domain.Entities;
using Sensus.Domain.Enums;
using Sensus.Domain.ValueObjects;

namespace Sensus.Application.UnitTests.Ai;

public class BudgetCalculatorTests
{
    [Fact]
    public void BuildPackage_ShouldReturnFailure_WhenBudgetIsZeroOrNegative()
    {
        var items = new[]
        {
            new RecommendedPackageItem("off-1", "Tacos al pastor", "m-1", "Taquería", 50m, 60m, 5, true)
        };

        var resultZero = BudgetCalculator.BuildPackage(0m, items);
        var resultNegative = BudgetCalculator.BuildPackage(-10m, items);

        resultZero.IsFailure.Should().BeTrue();
        resultNegative.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void BuildPackage_ShouldSelectItemsWithinBudget_AndEnforceInvariants()
    {
        var items = new[]
        {
            new RecommendedPackageItem("off-1", "Agua fresca", "m-1", "Taquería", 25m, 30m, 10, true),
            new RecommendedPackageItem("off-2", "Orden de tacos", "m-1", "Taquería", 75m, 90m, 5, true),
            new RecommendedPackageItem("off-3", "Postre gourmet", "m-1", "Taquería", 150m, 180m, 2, true)
        };

        var result = BudgetCalculator.BuildPackage(120m, items);

        result.IsSuccess.Should().BeTrue();
        var pkg = result.Value;
        pkg.TotalPriceMxn.Should().BeLessThanOrEqualTo(120m);
        pkg.TotalPriceMxn.Should().Be(100m); // 25 + 75
        pkg.Items.Should().HaveCount(2);
        pkg.SavingsMxn.Should().Be(20m); // (30 - 25) + (90 - 75)
    }

    [Fact]
    public void BuildPackage_ShouldIgnoreClosedMerchantsOrZeroStock()
    {
        var items = new[]
        {
            new RecommendedPackageItem("off-1", "Tacos", "m-1", "Taquería", 50m, 60m, 0, true), // Sin stock
            new RecommendedPackageItem("off-2", "Refresco", "m-1", "Taquería", 20m, 25m, 5, false) // Cerrado
        };

        var result = BudgetCalculator.BuildPackage(100m, items);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Ai.NoItemsWithinBudget");
    }

    [Fact]
    public void EstimateTurnCostUsd_ShouldCalculateCorrectlyForFastAndReasoningTiers()
    {
        // 1000 prompt tokens (200 cached, 800 uncached), 200 completion tokens
        var fastCost = BudgetCalculator.EstimateTurnCostUsd(AiRoutingTier.Fast, 1000, 200, 200);
        var reasoningCost = BudgetCalculator.EstimateTurnCostUsd(AiRoutingTier.Reasoning, 1000, 200, 200);

        fastCost.Should().BeGreaterThan(0m);
        reasoningCost.Should().BeGreaterThan(fastCost);
    }
}
