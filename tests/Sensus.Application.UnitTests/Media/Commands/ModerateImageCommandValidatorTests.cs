using FluentAssertions;
using Sensus.Application.Media.Commands.ModerateImage;
using Xunit;

namespace Sensus.Application.UnitTests.Media.Commands;

public sealed class ModerateImageCommandValidatorTests
{
    private readonly ModerateImageCommandValidator _validator = new();

    [Fact]
    public void Validate_WhenBase64IsValid_ShouldPass()
    {
        // Arrange
        var base64 = Convert.ToBase64String([1, 2, 3, 4, 5]);
        var command = new ModerateImageCommand(base64);

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WhenBase64HasDataUriPrefix_ShouldPass()
    {
        // Arrange
        var base64 = $"data:image/jpeg;base64,{Convert.ToBase64String([10, 20, 30])}";
        var command = new ModerateImageCommand(base64, "image/jpeg");

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_WhenBase64IsEmptyOrWhitespace_ShouldFail(string emptyBase64)
    {
        // Arrange
        var command = new ModerateImageCommand(emptyBase64);

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(ModerateImageCommand.ImageBase64));
    }

    [Fact]
    public void Validate_WhenBase64IsMalformed_ShouldFail()
    {
        // Arrange
        var command = new ModerateImageCommand("invalid-non-base64-payload!#%");

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("no es una cadena Base64 válida"));
    }
}
