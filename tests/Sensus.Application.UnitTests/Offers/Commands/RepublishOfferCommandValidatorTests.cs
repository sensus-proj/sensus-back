using FluentAssertions;
using Sensus.Application.Offers.Commands.RepublishOffer;

namespace Sensus.Application.UnitTests.Offers.Commands;

public class RepublishOfferCommandValidatorTests
{
    private readonly RepublishOfferCommandValidator _validator = new();
    private readonly DateTime _now = new(2026, 11, 12, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Validate_ShouldBeValid_WhenAllFieldsAreValid()
    {
        var command = new RepublishOfferCommand(
            Guid.NewGuid(),
            _now.AddHours(1),
            _now.AddDays(3),
            10);

        var result = _validator.Validate(command);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_ShouldFail_WhenOfferIdIsEmpty()
    {
        var command = new RepublishOfferCommand(
            Guid.Empty,
            _now.AddHours(1),
            _now.AddDays(3),
            10);

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(RepublishOfferCommand.OfferId));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Validate_ShouldFail_WhenStockIsLessThanOne(int stock)
    {
        var command = new RepublishOfferCommand(
            Guid.NewGuid(),
            _now.AddHours(1),
            _now.AddDays(3),
            stock);

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(RepublishOfferCommand.NewStock));
    }

    [Fact]
    public void Validate_ShouldFail_WhenEndsAtIsNotGreaterThanStartsAt()
    {
        var command = new RepublishOfferCommand(
            Guid.NewGuid(),
            _now.AddDays(2),
            _now.AddDays(1),
            10);

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(RepublishOfferCommand.NewEndsAt));
    }
}
