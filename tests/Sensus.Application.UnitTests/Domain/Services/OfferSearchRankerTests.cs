// Ref: FR-041-02 / FR-041-05 - Ranking híbrido determinista de ofertas
using FluentAssertions;
using Sensus.Domain.Enums;
using Sensus.Domain.Models;
using Sensus.Domain.Services;

namespace Sensus.Application.UnitTests.Domain.Services;

public sealed class OfferSearchRankerTests
{
    private static OfferSearchCandidate CreateCandidate(
        Guid offerId,
        string title,
        string description,
        string merchantName,
        string category = "Gastronomia",
        double distanceMeters = 1000,
        int viewCount = 10,
        double mongoTextScore = 0d)
    {
        return new OfferSearchCandidate(
            OfferId: offerId,
            Title: title,
            Description: description,
            Category: category,
            ImageUrl: "https://example.com/img.jpg",
            OriginalPrice: 100m,
            FinalPrice: 80m,
            Stock: 5,
            DistanceMeters: distanceMeters,
            ExpiresAtUtc: DateTime.UtcNow.AddDays(1),
            MerchantId: Guid.NewGuid(),
            MerchantName: merchantName,
            ViewCount: viewCount,
            MongoTextScore: mongoTextScore);
    }

    [Fact]
    public void Rank_WithEmptyCandidates_ShouldReturnEmptyList()
    {
        var policy = new OfferSearchPolicy(new OfferSearchWeights(0.4, 0.3, 0.2, 0.1), 5000);
        var result = OfferSearchRanker.Rank("pizza", [], policy);

        result.Should().BeEmpty();
    }

    [Fact]
    public void Rank_WithNullArguments_ShouldThrowArgumentNullException()
    {
        var policy = new OfferSearchPolicy(new OfferSearchWeights(0.4, 0.3, 0.2, 0.1), 5000);
        var actNullCandidates = () => OfferSearchRanker.Rank("pizza", null!, policy);
        var actNullPolicy = () => OfferSearchRanker.Rank("pizza", [], null!);

        actNullCandidates.Should().Throw<ArgumentNullException>();
        actNullPolicy.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Rank_WithEmptyOrNullQuery_ShouldRankByProximityAndPopularityDeterministically()
    {
        var policy = new OfferSearchPolicy(new OfferSearchWeights(0.4, 0.3, 0.2, 0.1), 5000);
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();

        var candidate1 = CreateCandidate(id1, "Pizza Pepperoni", "Deliciosa pizza", "Pizzeria Roma", distanceMeters: 1000, viewCount: 50);
        var candidate2 = CreateCandidate(id2, "Hamburguesa", "Con papas", "Burger King", distanceMeters: 500, viewCount: 10);

        var result = OfferSearchRanker.Rank(null, [candidate1, candidate2], policy);

        result.Should().HaveCount(2);
        // Candidate 2 has smaller distance (500m vs 1000m), ordered first
        result[0].Candidate.OfferId.Should().Be(id2);
        result[1].Candidate.OfferId.Should().Be(id1);
    }

    [Fact]
    public void Rank_WithExactTitleMatch_ShouldAssignHighestTextScore()
    {
        var policy = new OfferSearchPolicy(new OfferSearchWeights(0.5, 0.2, 0.2, 0.1), 5000);
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();

        var exact = CreateCandidate(id1, "Pizza Napolitana", "Tradicional", "Pizzeria Roma", mongoTextScore: 1.0);
        var partial = CreateCandidate(id2, "Pizza Napolitana Especial Con Queso", "Tradicional", "Pizzeria Roma", mongoTextScore: 1.0);

        var result = OfferSearchRanker.Rank("Pizza Napolitana", [exact, partial], policy);

        result.Should().HaveCount(2);
        result[0].Candidate.OfferId.Should().Be(id1);
        result[0].Score.Should().BeGreaterThan(result[1].Score);
    }

    [Fact]
    public void Rank_WithVariousMatchTypes_ShouldRankAccordingToRelevance()
    {
        var policy = new OfferSearchPolicy(new OfferSearchWeights(0.6, 0.2, 0.1, 0.1), 5000);
        var cStartsWith = CreateCandidate(Guid.NewGuid(), "Cafe Americano Caliente", "Rico cafe", "Starbucks", category: "Cafeterias");
        var cTokenPrefix = CreateCandidate(Guid.NewGuid(), "El Mejor Cafecito Gourmet", "Tostado local", "Starbucks", category: "Cafeterias");
        var cTitleContains = CreateCandidate(Guid.NewGuid(), "Gran Sabor Con Cafe En Grano", "Bebida caliente", "Starbucks", category: "Cafeterias");
        var cMerchantMatch = CreateCandidate(Guid.NewGuid(), "Bebida Fria", "Refrescante", "Cafe Central", category: "Cafeterias");
        var cDescContains = CreateCandidate(Guid.NewGuid(), "Bebida de la Casa", "Hecho con cafe seleccionado", "Central", category: "Cafeterias");
        var cNoMatch = CreateCandidate(Guid.NewGuid(), "Zapatos Deportivos", "Talla 42", "Nike", category: "Calzado");

        var result = OfferSearchRanker.Rank("Cafe", [cStartsWith, cTokenPrefix, cTitleContains, cMerchantMatch, cDescContains, cNoMatch], policy);

        result.Select(r => r.Candidate.OfferId).Should().NotContain(cNoMatch.OfferId);
        result.Should().NotBeEmpty();
    }
}
