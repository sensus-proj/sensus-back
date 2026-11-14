using FluentAssertions;
using Sensus.Application.Users.Commands.RegisterDevice;
using Xunit;

namespace Sensus.Application.UnitTests.Users.Commands.RegisterDevice;

public sealed class RegisterDeviceCommandValidatorTests
{
    private readonly RegisterDeviceCommandValidator _validator = new();

    [Theory]
    [InlineData("Android")]
    [InlineData("iOS")]
    [InlineData("Web")]
    public void Validate_WithValidPlatform_ShouldPass(string platform)
    {
        // Arrange
        var command = new RegisterDeviceCommand("device-123", platform, "token-12345678901234567890");

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_WithEmptyDeviceId_ShouldFail(string emptyDeviceId)
    {
        // Arrange
        var command = new RegisterDeviceCommand(emptyDeviceId, "Android", "token-12345678901234567890");

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(RegisterDeviceCommand.DeviceId));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_WithEmptyPlatform_ShouldFail(string emptyPlatform)
    {
        // Arrange
        var command = new RegisterDeviceCommand("device-123", emptyPlatform, "token-12345678901234567890");

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(RegisterDeviceCommand.Platform));
    }

    [Theory]
    [InlineData("Windows")]
    [InlineData("Linux")]
    [InlineData("macOS")]
    [InlineData("android")]
    [InlineData("ios")]
    public void Validate_WithInvalidPlatform_ShouldFail(string invalidPlatform)
    {
        // Arrange
        var command = new RegisterDeviceCommand("device-123", invalidPlatform, "token-12345678901234567890");

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(RegisterDeviceCommand.Platform));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_WithEmptyToken_ShouldFail(string emptyToken)
    {
        // Arrange
        var command = new RegisterDeviceCommand("device-123", "Android", emptyToken);

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(RegisterDeviceCommand.Token));
    }

    [Fact]
    public void Validate_WithTokenTooShort_ShouldFail()
    {
        // Arrange
        var command = new RegisterDeviceCommand("device-123", "Android", "short-token");

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(RegisterDeviceCommand.Token));
    }

    [Fact]
    public void Validate_WithDeviceIdOver100Chars_ShouldFail()
    {
        // Arrange
        var longDeviceId = new string('a', 101);
        var command = new RegisterDeviceCommand(longDeviceId, "Android", "token-12345678901234567890");

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(RegisterDeviceCommand.DeviceId));
    }
}
