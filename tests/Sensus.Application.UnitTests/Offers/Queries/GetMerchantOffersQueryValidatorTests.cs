using FluentAssertions;
using Sensus.Application.Offers.Queries.GetMerchantOffers;

namespace Sensus.Application.UnitTests.Offers.Queries;

public sealed class GetMerchantOffersQueryValidatorTests
{
    private readonly GetMerchantOffersQueryValidator _validator = new();

    [Fact]
    public void Validate_ShouldPass_WithDefaultValues()
    {
        var result = _validator.Validate(new GetMerchantOffersQuery());

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_ShouldPass_WithAllowedStatusAndPageSize()
    {
        var result = _validator.Validate(new GetMerchantOffersQuery("archived", 2, 100));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_ShouldFail_WhenPageSizeExceedsMaximum()
    {
        var result = _validator.Validate(new GetMerchantOffersQuery(PageSize: 200));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("tamaño de página"));
    }

    [Fact]
    public void Validate_ShouldFail_WhenPageIsBelowOne()
    {
        var result = _validator.Validate(new GetMerchantOffersQuery(Page: 0));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("página"));
    }

    [Fact]
    public void Validate_ShouldFail_WhenStatusIsNotKnown()
    {
        var result = _validator.Validate(new GetMerchantOffersQuery(StatusFilter: "NoExiste"));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("estado"));
    }
}
