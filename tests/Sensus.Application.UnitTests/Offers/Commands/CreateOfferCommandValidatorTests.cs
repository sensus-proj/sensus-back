using FluentAssertions;
using Sensus.Application.Offers.Commands.CreateOffer;

namespace Sensus.Application.UnitTests.Offers.Commands;

public class CreateOfferCommandValidatorTests
{
    private readonly CreateOfferCommandValidator _validator = new();
    private readonly DateTime _now = new(2026, 11, 12, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Validate_ShouldBeValid_WhenAllFieldsAreValid()
    {
        var command = new CreateOfferCommand(
            "Hamburguesa Doble 2x1",
            "Deliciosa hamburguesa con queso y papas incluidas.",
            150m,
            20,
            10,
            _now.AddHours(1),
            _now.AddDays(3),
            "https://cloudinary.com/img.png");

        var result = _validator.Validate(command);

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("AB")]
    public void Validate_ShouldFail_WhenTitleIsTooShortOrEmpty(string title)
    {
        var command = new CreateOfferCommand(
            title,
            "Descripción válida de prueba.",
            100m,
            10,
            5,
            _now.AddHours(1),
            _now.AddDays(2));

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateOfferCommand.Title));
    }

    [Fact]
    public void Validate_ShouldFail_WhenTitleExceedsMaxLength()
    {
        var longTitle = new string('A', 101);
        var command = new CreateOfferCommand(
            longTitle,
            "Descripción válida de prueba.",
            100m,
            10,
            5,
            _now.AddHours(1),
            _now.AddDays(2));

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateOfferCommand.Title));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("1234")]
    public void Validate_ShouldFail_WhenDescriptionIsTooShortOrEmpty(string description)
    {
        var command = new CreateOfferCommand(
            "Título válido",
            description,
            100m,
            10,
            5,
            _now.AddHours(1),
            _now.AddDays(2));

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateOfferCommand.Description));
    }

    [Fact]
    public void Validate_ShouldFail_WhenDescriptionExceedsMaxLength()
    {
        var longDesc = new string('D', 501);
        var command = new CreateOfferCommand(
            "Título válido",
            longDesc,
            100m,
            10,
            5,
            _now.AddHours(1),
            _now.AddDays(2));

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateOfferCommand.Description));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public void Validate_ShouldFail_WhenOriginalPriceIsZeroOrNegative(decimal price)
    {
        var command = new CreateOfferCommand(
            "Título válido",
            "Descripción válida de prueba.",
            price,
            10,
            5,
            _now.AddHours(1),
            _now.AddDays(2));

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateOfferCommand.OriginalPrice));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(91)]
    [InlineData(-5)]
    public void Validate_ShouldFail_WhenDiscountIsOutOfRange(int discount)
    {
        var command = new CreateOfferCommand(
            "Título válido",
            "Descripción válida de prueba.",
            100m,
            discount,
            5,
            _now.AddHours(1),
            _now.AddDays(2));

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateOfferCommand.DiscountPercentage));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_ShouldFail_WhenStockIsLessThanOne(int stock)
    {
        var command = new CreateOfferCommand(
            "Título válido",
            "Descripción válida de prueba.",
            100m,
            10,
            stock,
            _now.AddHours(1),
            _now.AddDays(2));

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateOfferCommand.Stock));
    }

    [Fact]
    public void Validate_ShouldFail_WhenEndsAtIsNotGreaterThanStartsAt()
    {
        var command = new CreateOfferCommand(
            "Título válido",
            "Descripción válida de prueba.",
            100m,
            10,
            5,
            _now.AddDays(3),
            _now.AddDays(1));

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateOfferCommand.EndsAt));
    }
}
