using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Common.Models;
using Sensus.Application.Common.Settings;
using Sensus.Application.Offers.DTOs;
using Sensus.Application.Offers.Queries.SearchOffers;
using Sensus.Domain.Enums;
using Sensus.Domain.Models;
using Sensus.Domain.Policies;
using Sensus.Domain.Repositories;
using Sensus.Domain.Services;

namespace Sensus.Application.UnitTests.Offers.Discovery;

public sealed class OfferSearchTests
{
    private static readonly DateTime Now = new(2026, 11, 20, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void OfferTextNormalizer_ShouldLowercaseRemoveAccentsAndCollapseSpaces()
    {
        OfferTextNormalizer.Normalize("  Panadería   Artesanal\tDulce  ").Should().Be("panaderia artesanal dulce");
        OfferTextNormalizer.Normalize("LÁCTEOS Orgánicos").Should().Be("lacteos organicos");
        OfferTextNormalizer.BuildCategorySearchText("Bakery").Should().Contain("Bakery").And.Contain("Panadería").And.Contain("panaderia");
    }

    [Fact]
    public void OfferTextNormalizer_ExtractMatchingCategories_ShouldRecognizeSpanishAndPrefix()
    {
        OfferTextNormalizer.ExtractMatchingCategories("panaderia").Should().Contain(OfferCategory.Bakery);
        OfferTextNormalizer.ExtractMatchingCategories("pa").Should().Contain(OfferCategory.Bakery);
        OfferTextNormalizer.ExtractMatchingCategories("lacteos").Should().Contain(OfferCategory.Dairy);
    }

    [Fact]
    public void OfferSearchRanker_ShouldBeDeterministic_AndMeet90PercentCategoryAccuracyInTop10()
    {
        var candidates = new List<OfferSearchCandidate>();
        for (int i = 0; i < 10; i++)
        {
            candidates.Add(new OfferSearchCandidate(
                Guid.Parse($"00000000-0000-0000-0000-0000000001{i:00}"),
                $"Bolsa sorpresa {i + 1}",
                "Producto fresco de horno",
                OfferCategory.Bakery.ToString(),
                null,
                100m,
                60m,
                5,
                500 + (i * 100),
                Now.AddDays(2),
                Guid.NewGuid(),
                "La Espiga",
                20 - i,
                1.2d));
        }

        // Ruido de otra categoría que solo menciona la palabra en descripción
        candidates.Add(new OfferSearchCandidate(
            Guid.Parse("00000000-0000-0000-0000-000000000901"),
            "Refresco familiar",
            "Ideal junto a panadería tradicional",
            OfferCategory.Beverages.ToString(),
            null,
            50m,
            35m,
            10,
            100d,
            Now.AddDays(2),
            Guid.NewGuid(),
            "Bebidas Sur",
            50,
            1.5d));

        var policy = new OfferSearchPolicy(new OfferSearchWeights(), 10_000);

        IReadOnlyList<RankedOfferSearchEntry> first = OfferSearchRanker.Rank("panadería", candidates, policy);
        IReadOnlyList<RankedOfferSearchEntry> second = OfferSearchRanker.Rank("panadería", candidates, policy);

        first.Select(e => e.Candidate.OfferId).Should().Equal(second.Select(e => e.Candidate.OfferId));

        int exactCategoryInTop10 = first.Take(10)
            .Count(e => string.Equals(e.Candidate.Category, nameof(OfferCategory.Bakery), StringComparison.OrdinalIgnoreCase));
        exactCategoryInTop10.Should().BeGreaterThanOrEqualTo(9); // NFR-041-02 >= 90%
    }

    [Fact]
    public void OfferSearchRanker_EmptyQuery_ShouldOrderByProximityFirst()
    {
        var farPopular = new OfferSearchCandidate(Guid.NewGuid(), "Lejos", "Desc", "Bakery", null, 50m, 30m, 3, 2500d, Now.AddDays(1), Guid.NewGuid(), "Comercio A", 500);
        var nearNew = new OfferSearchCandidate(Guid.NewGuid(), "Cerca", "Desc", "Food", null, 50m, 30m, 3, 200d, Now.AddDays(1), Guid.NewGuid(), "Comercio B", 0);

        IReadOnlyList<RankedOfferSearchEntry> ranked = OfferSearchRanker.Rank(
            null,
            [farPopular, nearNew],
            new OfferSearchPolicy(new OfferSearchWeights(), 10_000));

        ranked.Select(r => r.Candidate.OfferId).Should().Equal(nearNew.OfferId, farPopular.OfferId);
    }

    [Theory]
    [InlineData("a")]
    [InlineData("   a   ")]
    [InlineData("line\nbreak")]
    public void SearchOffersQueryValidator_ShouldRejectInvalidQueries(string badQuery)
    {
        var validator = new SearchOffersQueryValidator();
        var result = validator.Validate(new SearchOffersQuery(badQuery, 19.4, -99.1));
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void SearchOffersQueryValidator_ShouldReject81CharQueryAndInvalidCoordinatesOrPageSize()
    {
        var validator = new SearchOffersQueryValidator();
        validator.Validate(new SearchOffersQuery(new string('p', 81), 19.4, -99.1)).IsValid.Should().BeFalse();
        validator.Validate(new SearchOffersQuery("pan", 91, -99.1)).IsValid.Should().BeFalse();
        validator.Validate(new SearchOffersQuery("pan", 19.4, 181)).IsValid.Should().BeFalse();
        validator.Validate(new SearchOffersQuery("pan", 19.4, -99.1, RadiusKm: 51)).IsValid.Should().BeFalse();
        validator.Validate(new SearchOffersQuery("pan", 19.4, -99.1, PageSize: 101)).IsValid.Should().BeFalse();
        validator.Validate(new SearchOffersQuery("pan", 19.4, -99.1, Page: 0)).IsValid.Should().BeFalse();
        validator.Validate(new SearchOffersQuery("", 19.4, -99.1)).IsValid.Should().BeTrue();
        validator.Validate(new SearchOffersQuery("pa", 19.4, -99.1)).IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task SearchOffersQueryHandler_ShouldClampGuestRadiusToTwoKilometers()
    {
        IOfferRepository repo = Substitute.For<IOfferRepository>();
        ICurrentUserService user = Substitute.For<ICurrentUserService>();
        user.IsGuest.Returns(true);
        IDateTimeProvider clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(Now);

        int? capturedRadius = null;
        repo.SearchOffersHybridAsync(
                Arg.Any<string?>(),
                Arg.Any<string?>(),
                Arg.Any<double>(),
                Arg.Any<double>(),
                Arg.Do<int>(r => capturedRadius = r),
                Arg.Any<int>(),
                Arg.Any<DateTime>(),
                Arg.Any<CancellationToken>())
            .Returns(Array.Empty<OfferSearchCandidate>());

        var handler = new SearchOffersQueryHandler(
            repo,
            user,
            clock,
            Options.Create(new OfferSearchSettings()),
            NullLogger<SearchOffersQueryHandler>.Instance);

        var result = await handler.Handle(new SearchOffersQuery("pan", 19.43, -99.13, RadiusKm: 15), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        capturedRadius.Should().Be(GuestRadiusPolicy.GuestMaxRadiusMeters);
    }

    [Fact]
    public void OfferSearchItemDto_Mapping_ShouldMapExplicitlyAndNormalizeLegacyCategory()
    {
        var candidate = new OfferSearchCandidate(
            Guid.NewGuid(),
            "Conchas",
            "Recién horneadas",
            "Panadería",
            "https://cdn.example/img.jpg",
            40m,
            24m,
            4,
            123.6,
            Now.AddDays(1),
            Guid.NewGuid(),
            "La Espiga",
            7,
            2.1);
        var dto = new RankedOfferSearchEntry(candidate, 0.9123).ToDto();

        dto.OfferId.Should().Be(candidate.OfferId);
        dto.Title.Should().Be("Conchas");
        dto.Category.Should().Be(nameof(OfferCategory.Bakery));
        dto.DistanceMeters.Should().Be(124);
        dto.FinalPrice.Should().Be(24m);
        dto.Score.Should().Be(0.9123);
    }
}
