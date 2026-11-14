using FluentAssertions;
using Sensus.Application.Users.Commands.UpdateNotificationSettings;
using Xunit;

namespace Sensus.Application.UnitTests.Users.Commands.UpdateNotificationSettings;

public sealed class UpdateNotificationSettingsCommandValidatorTests
{
    private readonly UpdateNotificationSettingsCommandValidator _validator = new();

    [Fact]
    public void Validate_WithAllNullValues_ShouldPass()
    {
        // Arrange
        var command = new UpdateNotificationSettingsCommand(null, null, null, null);

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithValidValues_ShouldPass()
    {
        // Arrange
        var command = new UpdateNotificationSettingsCommand(true, false, true, 25);

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(51)]
    [InlineData(-1)]
    [InlineData(100)]
    public void Validate_WithRadiusKmOutOfRange_ShouldFail(int invalidRadiusKm)
    {
        // Arrange
        var command = new UpdateNotificationSettingsCommand(null, null, null, invalidRadiusKm);

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(UpdateNotificationSettingsCommand.RadiusKm));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(50)]
    public void Validate_WithRadiusKmAtBoundaries_ShouldPass(int validRadiusKm)
    {
        // Arrange
        var command = new UpdateNotificationSettingsCommand(null, null, null, validRadiusKm);

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithOnlyPushEnabled_ShouldPass()
    {
        // Arrange
        var command = new UpdateNotificationSettingsCommand(false, null, null, null);

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithOnlyOfferAlerts_ShouldPass()
    {
        // Arrange
        var command = new UpdateNotificationSettingsCommand(null, true, null, null);

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithOnlySocialAlerts_ShouldPass()
    {
        // Arrange
        var command = new UpdateNotificationSettingsCommand(null, null, false, null);

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeTrue();
    }
}
