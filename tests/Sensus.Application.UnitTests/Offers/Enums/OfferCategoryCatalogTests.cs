using FluentAssertions;
using Sensus.Domain.Enums;

namespace Sensus.Application.UnitTests.Offers.Enums;

public sealed class OfferCategoryCatalogTests
{
    [Fact]
    public void Catalog_ShouldExposeExactNineProductCategories()
    {
        OfferCategoryCatalog.All.Should().HaveCount(9);
        OfferCategoryCatalog.Names.Should().BeEquivalentTo(
            [
                "Grocery",
                "CleaningHome",
                "Perishables",
                "Dairy",
                "Food",
                "Bakery",
                "Beverages",
                "Pets",
                "Pharmacy"
            ],
            options => options.WithStrictOrdering());
    }

    [Theory]
    [InlineData("Grocery", OfferCategory.Grocery)]
    [InlineData("CleaningHome", OfferCategory.CleaningHome)]
    [InlineData("Perishables", OfferCategory.Perishables)]
    [InlineData("Dairy", OfferCategory.Dairy)]
    [InlineData("Food", OfferCategory.Food)]
    [InlineData("Bakery", OfferCategory.Bakery)]
    [InlineData("Beverages", OfferCategory.Beverages)]
    [InlineData("Pets", OfferCategory.Pets)]
    [InlineData("Pharmacy", OfferCategory.Pharmacy)]
    [InlineData(" bakery ", OfferCategory.Bakery)]
    public void TryParse_ShouldSucceed_ForCatalogCodes(string raw, OfferCategory expected)
    {
        bool parsed = OfferCategoryCatalog.TryParse(raw, out OfferCategory actual);

        parsed.Should().BeTrue();
        actual.Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("1")]
    [InlineData("99")]
    [InlineData("All")]
    [InlineData("Moda")]
    [InlineData("Panadería")]
    public void TryParse_ShouldFail_ForInvalidOrLegacyValues(string? raw)
    {
        bool parsed = OfferCategoryCatalog.TryParse(raw, out _);

        parsed.Should().BeFalse();
    }

    [Theory]
    [InlineData("Abarrotes", OfferCategory.Grocery)]
    [InlineData("Limpieza y Hogar", OfferCategory.CleaningHome)]
    [InlineData("limpieza y hogar", OfferCategory.CleaningHome)]
    [InlineData("Perecederos", OfferCategory.Perishables)]
    [InlineData("Lácteos", OfferCategory.Dairy)]
    [InlineData("Lacteos", OfferCategory.Dairy)]
    [InlineData("Comida", OfferCategory.Food)]
    [InlineData("Panadería", OfferCategory.Bakery)]
    [InlineData("Panaderia", OfferCategory.Bakery)]
    [InlineData("Bebidas", OfferCategory.Beverages)]
    [InlineData("Mascotas", OfferCategory.Pets)]
    [InlineData("Farmacia", OfferCategory.Pharmacy)]
    [InlineData("Bakery", OfferCategory.Bakery)]
    public void TryNormalizeLegacy_ShouldMapSpanishAndCanonicalNames(string raw, OfferCategory expected)
    {
        bool normalized = OfferCategoryCatalog.TryNormalizeLegacy(raw, out OfferCategory actual);

        normalized.Should().BeTrue();
        actual.Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Todo")]
    [InlineData("Moda")]
    [InlineData("Tecnologia")]
    public void TryNormalizeLegacy_ShouldReturnFalse_ForUnrecognizedValues(string? raw)
    {
        bool normalized = OfferCategoryCatalog.TryNormalizeLegacy(raw, out _);

        normalized.Should().BeFalse();
    }
}
