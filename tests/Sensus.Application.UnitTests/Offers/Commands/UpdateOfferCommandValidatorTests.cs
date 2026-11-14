using FluentAssertions;
using Sensus.Application.Offers.Commands.UpdateOffer;

namespace Sensus.Application.UnitTests.Offers.Commands;

public class UpdateOfferCommandValidatorTests
{
    private readonly UpdateOfferCommandValidator _validator = new();

    [Fact]
    public void Validate_ShouldPass_WhenAllFieldsAreValid()
    {
        var command = new UpdateOfferCommand(
            Guid.NewGuid(),
            "Título Válido",
            "Descripción suficientemente larga",
            100.00m,
            15,
            10,
            DateTime.UtcNow.AddHours(1),
            DateTime.UtcNow.AddDays(2),
            "https://cloudinary.com/image.jpg",
            1);

        var result = _validator.Validate(command);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_ShouldFail_WhenOfferIdIsEmpty()
    {
        var command = new UpdateOfferCommand(
            Guid.Empty,
            "Título Válido",
            "Descripción suficientemente larga",
            100.00m,
            15,
            10,
            DateTime.UtcNow.AddHours(1),
            DateTime.UtcNow.AddDays(2),
            null,
            1);

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(UpdateOfferCommand.OfferId));
    }

    [Fact]
    public void Validate_ShouldFail_WhenExpectedVersionIsZeroOrNegative()
    {
        var command = new UpdateOfferCommand(
            Guid.NewGuid(),
            "Título Válido",
            "Descripción suficientemente larga",
            100.00m,
            15,
            10,
            DateTime.UtcNow.AddHours(1),
            DateTime.UtcNow.AddDays(2),
            null,
            ExpectedVersion: 0);

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(UpdateOfferCommand.ExpectedVersion));
    }
}
