using FluentAssertions;
using Sensus.Application.Offers.Queries.GetNearbyOffers;

namespace Sensus.Application.UnitTests.Offers.Queries;

public class GetNearbyOffersQueryValidatorTests
{
    private readonly GetNearbyOffersQueryValidator _validator = new();

    [Fact]
    public void Validate_ShouldPass_WhenQueryIsValid()
    {
        var query = new GetNearbyOffersQuery(-12.05, -77.04, 5, "Restaurantes", 1, 20);

        _validator.Validate(query).IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(-91)]
    [InlineData(91)]
    public void Validate_ShouldFail_WhenLatitudeIsOutOfRange(double latitude)
    {
        var query = new GetNearbyOffersQuery(latitude, -77.04);

        var result = _validator.Validate(query);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(GetNearbyOffersQuery.Latitude));
    }

    [Theory]
    [InlineData(-181)]
    [InlineData(181)]
    public void Validate_ShouldFail_WhenLongitudeIsOutOfRange(double longitude)
    {
        var query = new GetNearbyOffersQuery(-12.05, longitude);

        var result = _validator.Validate(query);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(GetNearbyOffersQuery.Longitude));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public void Validate_ShouldFail_WhenPageSizeIsOutOfRange(int pageSize)
    {
        var query = new GetNearbyOffersQuery(-12.05, -77.04, null, null, 1, pageSize);

        var result = _validator.Validate(query);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(GetNearbyOffersQuery.PageSize));
    }

    [Fact]
    public void Validate_ShouldFail_WhenPageIsZero()
    {
        var query = new GetNearbyOffersQuery(-12.05, -77.04, null, null, 0, 20);

        var result = _validator.Validate(query);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(GetNearbyOffersQuery.Page));
    }

    [Fact]
    public void Validate_ShouldFail_WhenRadiusExceedsTechnicalLimit()
    {
        var query = new GetNearbyOffersQuery(-12.05, -77.04, 80);

        var result = _validator.Validate(query);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(GetNearbyOffersQuery.RadiusKm));
    }

    [Fact]
    public void Validate_ShouldFail_WhenCategoryIsNotInCatalog()
    {
        var query = new GetNearbyOffersQuery(-12.05, -77.04, null, "NoExiste");

        var result = _validator.Validate(query);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(GetNearbyOffersQuery.Category));
    }
}
