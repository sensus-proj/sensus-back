using FluentAssertions;
using Sensus.Domain.Entities;
using Sensus.Domain.Enums;
using Sensus.TestKit.Fakes;

namespace Sensus.IntegrationTests.Offers;

public class InMemoryOfferRepositoryTests
{
    private readonly InMemoryOfferRepository _repository = new();

    [Fact]
    public async Task SearchAsync_ShouldFilterByVigenciaStockAndText()
    {
        // Arrange
        var merchantId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        var activeOffer = Offer.Create(
            merchantId,
            "Pizza Margherita Especial",
            "Deliciosa pizza con queso mozarella y albahaca",
            200m,
            20,
            5,
            now.AddDays(-1),
            now.AddDays(2),
            "https://img.com/pizza.jpg",
            19.4,
            -99.1,
            now,
            MerchantCategory.Restaurantes.ToString()).Value;

        var expiredOffer = Offer.Reconstitute(
            Guid.NewGuid(),
            merchantId,
            "Pizza Hawaiana",
            "Pizza con jamón y piña",
            180m,
            10,
            162m,
            5,
            now.AddDays(-5),
            now.AddDays(-1),
            OfferStatus.Expired,
            "https://img.com/pizza2.jpg",
            19.4,
            -99.1,
            now.AddDays(-5),
            category: MerchantCategory.Restaurantes.ToString());

        var outOfStockOffer = Offer.Reconstitute(
            Guid.NewGuid(),
            merchantId,
            "Pizza Pepperoni",
            "Pizza clásica con pepperoni",
            210m,
            15,
            178.5m,
            0,
            now.AddDays(-1),
            now.AddDays(2),
            OfferStatus.Published,
            "https://img.com/pizza3.jpg",
            19.4,
            -99.1,
            now.AddDays(-1),
            category: MerchantCategory.Restaurantes.ToString());

        var expensiveOffer = Offer.Create(
            merchantId,
            "Pizza Gourmet Trufas",
            "Pizza con aceite de trufas",
            500m,
            10,
            3,
            now.AddDays(-1),
            now.AddDays(2),
            "https://img.com/pizza4.jpg",
            19.4,
            -99.1,
            now,
            MerchantCategory.Restaurantes.ToString()).Value;

        await _repository.AddAsync(activeOffer);
        await _repository.AddAsync(expiredOffer);
        await _repository.AddAsync(outOfStockOffer);
        await _repository.AddAsync(expensiveOffer);

        // Act - Search by query "Margherita"
        var queryResults = await _repository.SearchAsync("Margherita", null, null, 10);
        queryResults.Should().ContainSingle();
        queryResults[0].Id.Should().Be(activeOffer.Id);

        // Act - Search by category
        var categoryResults = await _repository.SearchAsync(null, MerchantCategory.Restaurantes, null, 10);
        categoryResults.Should().HaveCount(2); // activeOffer and expensiveOffer
        categoryResults.Should().NotContain(o => o.Id == expiredOffer.Id);
        categoryResults.Should().NotContain(o => o.Id == outOfStockOffer.Id);

        // Act - Search with maxBudget
        var budgetResults = await _repository.SearchAsync(null, null, 200m, 10);
        budgetResults.Should().ContainSingle();
        budgetResults[0].Id.Should().Be(activeOffer.Id);
    }

    [Fact]
    public async Task GetByMerchantIdPagedAsync_ShouldReturnPagedResultsAndTotalCount()
    {
        // Arrange
        var merchantId = Guid.NewGuid();
        var otherMerchantId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        for (var i = 0; i < 5; i++)
        {
            var offer = Offer.Create(
                merchantId,
                $"Oferta {i}",
                $"Descripción {i}",
                100m,
                10,
                5,
                now.AddDays(-1),
                now.AddDays(1),
                "https://img.com/1.jpg",
                19.4,
                -99.1,
                now,
                MerchantCategory.Hogar.ToString()).Value;

            await _repository.AddAsync(offer);
        }

        var otherOffer = Offer.Create(
            otherMerchantId,
            "Oferta ajena",
            "Descripción ajena",
            100m,
            10,
            5,
            now.AddDays(-1),
            now.AddDays(1),
            "https://img.com/2.jpg",
            19.4,
            -99.1,
            now,
            MerchantCategory.Hogar.ToString()).Value;

        await _repository.AddAsync(otherOffer);

        // Act - Page 1 with pageSize 3
        var (page1Items, totalCount) = await _repository.GetByMerchantIdPagedAsync(merchantId, null, 1, 3);

        // Assert
        totalCount.Should().Be(5);
        page1Items.Should().HaveCount(3);

        // Act - Page 2 with pageSize 3
        var (page2Items, _) = await _repository.GetByMerchantIdPagedAsync(merchantId, null, 2, 3);
        page2Items.Should().HaveCount(2);
    }
}
