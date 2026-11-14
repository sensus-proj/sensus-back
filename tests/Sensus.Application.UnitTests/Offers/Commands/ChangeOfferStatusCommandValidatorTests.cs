using FluentAssertions;
using Sensus.Application.Offers.Commands.ChangeOfferStatus;

namespace Sensus.Application.UnitTests.Offers.Commands;

public class ChangeOfferStatusCommandValidatorTests
{
    private readonly ChangeOfferStatusCommandValidator _validator = new();

    [Theory]
    [InlineData("Published")]
    [InlineData("Paused")]
    [InlineData("Archived")]
    [InlineData("published")]
    [InlineData("PAUSED")]
    public void Validate_ShouldPass_WhenStatusIsAllowed(string status)
    {
        var command = new ChangeOfferStatusCommand(Guid.NewGuid(), status);
        var result = _validator.Validate(command);
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("Expired")]
    [InlineData("Draft")]
    [InlineData("Deleted")]
    [InlineData("Active")]
    public void Validate_ShouldFail_WhenStatusIsNotAllowed(string status)
    {
        var command = new ChangeOfferStatusCommand(Guid.NewGuid(), status);
        var result = _validator.Validate(command);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(ChangeOfferStatusCommand.NewStatus));
    }

    [Fact]
    public void Validate_ShouldFail_WhenOfferIdIsEmpty()
    {
        var command = new ChangeOfferStatusCommand(Guid.Empty, "Paused");
        var result = _validator.Validate(command);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(ChangeOfferStatusCommand.OfferId));
    }
}
