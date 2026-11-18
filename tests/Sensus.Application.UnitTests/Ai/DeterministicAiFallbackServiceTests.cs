using FluentAssertions;
using NSubstitute;
using Sensus.Application.Ai.Services;
using Sensus.Domain.Entities;
using Sensus.Domain.Enums;
using Sensus.Domain.Repositories;

namespace Sensus.Application.UnitTests.Ai;

public class DeterministicAiFallbackServiceTests
{
    private readonly IOfferRepository _offerRepository = Substitute.For<IOfferRepository>();

    [Fact]
    public async Task BuildFallbackAsync_WithBudget_ShouldReturnPackageWhenOffersAvailable()
    {
        // Arrange
        var service = new DeterministicAiFallbackService(_offerRepository);
        var now = DateTime.UtcNow;
        var merchantId = Guid.NewGuid();

        var offer1 = Offer.Create(merchantId, "Tacos Pastor", "5 tacos con piña", 80m, 10, 5, now.AddDays(-1), now.AddDays(1)).Value;
        var offer2 = Offer.Create(merchantId, "Agua Horchata", "1 litro fresca", 35m, 10, 5, now.AddDays(-1), now.AddDays(1)).Value;

        _offerRepository.SearchAsync(null, null, 150m, 10, Arg.Any<CancellationToken>())
            .Returns(new List<Offer> { offer1, offer2 });

        // Act
        var result = await service.BuildFallbackAsync("Quiero comer algo rico con 150 pesos", Guid.NewGuid(), 150m);

        // Assert
        result.Reply.Should().Contain("modo de alta demanda");
        result.RecommendedPackage.Should().NotBeNull();
        result.RecommendedPackage!.TotalPriceMxn.Should().BeLessThanOrEqualTo(150m);
        result.OffersCited.Should().HaveCount(2);
    }

    [Fact]
    public async Task BuildFallbackAsync_WithoutBudget_ShouldReturnHelpfulDefaultGuidance()
    {
        // Arrange
        var service = new DeterministicAiFallbackService(_offerRepository);

        // Act
        var result = await service.BuildFallbackAsync("¿Cómo funciona la app?", Guid.NewGuid(), null);

        // Assert
        result.Reply.Should().Contain("Sensi IA");
        result.Reply.Should().Contain("Ofertas");
        result.RecommendedPackage.Should().BeNull();
        result.OffersCited.Should().BeEmpty();
    }
}
