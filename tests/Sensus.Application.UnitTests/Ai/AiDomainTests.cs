using FluentAssertions;
using Sensus.Domain.Entities;
using Sensus.Domain.Enums;
using Sensus.Domain.ValueObjects;
using Xunit;

namespace Sensus.Application.UnitTests.Ai;

public sealed class AiDomainTests
{
    [Fact]
    public void AiConversation_Create_WhenValid_SetsPropertiesAndActiveStatus()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        // Act
        var result = AiConversation.Create(
            userId,
            RoleType.Consumidor,
            "gpt-4o-mini",
            AiRoutingTier.Fast,
            "1.0.0",
            "home_map",
            now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var conv = result.Value;
        conv.OwnerUserId.Should().Be(userId);
        conv.OwnerRole.Should().Be(RoleType.Consumidor);
        conv.Status.Should().Be(AiConversationStatus.Active);
        conv.AssignedModel.Should().Be("gpt-4o-mini");
        conv.RoutingTier.Should().Be(AiRoutingTier.Fast);
        conv.MarkdownVersion.Should().Be("1.0.0");
        conv.LastScreenContext.Should().Be("home_map");
        conv.MessageCount.Should().Be(0);
    }

    [Fact]
    public void AiConversation_Create_WhenOwnerOrModelMissing_Fails()
    {
        // Act
        var res1 = AiConversation.Create(Guid.Empty, RoleType.Consumidor, "gpt-4o-mini", AiRoutingTier.Fast, "1.0.0", null, DateTime.UtcNow);
        var res2 = AiConversation.Create(Guid.NewGuid(), RoleType.Consumidor, "", AiRoutingTier.Fast, "1.0.0", null, DateTime.UtcNow);

        // Assert
        res1.IsFailure.Should().BeTrue();
        res2.IsFailure.Should().BeTrue();
    }

    [Theory]
    [InlineData("Invitado")]
    [InlineData("invitado")]
    [InlineData("Admin")]
    [InlineData("")]
    [InlineData("SuperUser")]
    public void AiConversation_Create_WhenRoleIsInvitadoOrInvalid_ReturnsFailure(string invalidRole)
    {
        // Act
        var result = AiConversation.Create(
            Guid.NewGuid(),
            invalidRole,
            "gpt-4o-mini",
            AiRoutingTier.Fast,
            "1.0.0",
            null,
            DateTime.UtcNow);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Ai.InvalidRole");
    }

    [Fact]
    public void RecommendedPackage_Create_WhenDuplicateOfferIds_ReturnsFailure()
    {
        // Arrange
        var items = new[]
        {
            new RecommendedPackageItem("off-1", "Arroz 1kg", "m-1", "Tienda", 20m, 25m, 10, true),
            new RecommendedPackageItem("off-1", "Arroz 1kg repetido", "m-1", "Tienda", 20m, 25m, 10, true)
        };

        // Act
        var result = RecommendedPackage.Create(100m, items);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Ai.DuplicateOfferInPackage");
    }

    [Fact]
    public void AiConversation_IncrementMessageCount_AndArchive_UpdatesProperties()
    {
        // Arrange
        var conv = AiConversation.Create(Guid.NewGuid(), RoleType.Consumidor, "gpt-4o-mini", AiRoutingTier.Fast, "1.0.0", null, DateTime.UtcNow).Value;

        // Act
        conv.IncrementMessageCount(DateTime.UtcNow);
        conv.Archive(DateTime.UtcNow);

        // Assert
        conv.MessageCount.Should().Be(1);
        conv.Status.Should().Be(AiConversationStatus.Archived);
    }

    [Fact]
    public void RecommendedPackage_Create_WhenExceedsBudget_ReturnsFailure()
    {
        // Arrange
        var items = new[]
        {
            new RecommendedPackageItem("off-1", "Arroz 1kg", "m-1", "Tienda Don Pepe", 30m, 35m, 10, true),
            new RecommendedPackageItem("off-2", "Aceite 1L", "m-1", "Tienda Don Pepe", 50m, 55m, 5, true),
            new RecommendedPackageItem("off-3", "Frijol 1kg", "m-1", "Tienda Don Pepe", 35m, 40m, 8, true)
        };

        // Act: Total = 115, Presupuesto = 100
        var result = RecommendedPackage.Create(100m, items);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Ai.BudgetExceeded");
    }

    [Fact]
    public void RecommendedPackage_Create_WhenWithinBudget_CalculatesTotalAndSavings()
    {
        // Arrange
        var items = new[]
        {
            new RecommendedPackageItem("off-1", "Arroz 1kg", "m-1", "Tienda Don Pepe", 24m, 30m, 10, true),
            new RecommendedPackageItem("off-2", "Frijol 1kg", "m-1", "Tienda Don Pepe", 29m, 35m, 5, true),
            new RecommendedPackageItem("off-3", "Detergente 1kg", "m-1", "Tienda Don Pepe", 39m, 45m, 8, true)
        };

        // Act: Total = 92, Regular = 110, Presupuesto = 100
        var result = RecommendedPackage.Create(100m, items);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.TotalPriceMxn.Should().Be(92m);
        result.Value.SavingsMxn.Should().Be(18m);
        result.Value.BudgetMxn.Should().Be(100m);
    }

    [Fact]
    public void RecommendedPackage_Create_WhenItemClosedOrOutOfStock_ReturnsFailure()
    {
        // Arrange
        var closedItem = new[] { new RecommendedPackageItem("off-1", "Arroz", "m-1", "Tienda", 20m, 25m, 10, false) };
        var outOfStockItem = new[] { new RecommendedPackageItem("off-1", "Arroz", "m-1", "Tienda", 20m, 25m, 0, true) };

        // Act & Assert
        RecommendedPackage.Create(50m, closedItem).IsFailure.Should().BeTrue();
        RecommendedPackage.Create(50m, outOfStockItem).IsFailure.Should().BeTrue();
    }

    [Fact]
    public void BudgetCalculator_BuildPackage_GreedySelectsItemsWithinBudget()
    {
        // Arrange
        var candidates = new[]
        {
            new RecommendedPackageItem("off-1", "Sal 1kg", "m-1", "Tienda", 15m, 18m, 10, true),
            new RecommendedPackageItem("off-2", "Arroz 1kg", "m-1", "Tienda", 25m, 30m, 5, true),
            new RecommendedPackageItem("off-3", "Aceite 1L", "m-1", "Tienda", 45m, 50m, 2, true),
            new RecommendedPackageItem("off-4", "Jamón carisimo", "m-1", "Tienda", 120m, 130m, 1, true)
        };

        // Act: Con 50 MXN alcanza para Sal (15) + Arroz (25) = 40 <= 50
        var result = BudgetCalculator.BuildPackage(50m, candidates);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.TotalPriceMxn.Should().Be(40m);
        result.Value.Items.Should().HaveCount(2);
    }
}
