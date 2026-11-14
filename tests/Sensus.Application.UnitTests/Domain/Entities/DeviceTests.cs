using FluentAssertions;
using Sensus.Domain.Entities;
using Xunit;

namespace Sensus.Application.UnitTests.Domain.Entities;

public sealed class DeviceTests
{
    [Fact]
    public void Create_WithValidParameters_ShouldSucceed()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var deviceId = "device-abc-123";
        var platform = "Android";
        var token = "fcm-token-xyz789-abcdef-1234567890";
        var registeredAt = new DateTime(2026, 11, 13, 10, 0, 0, DateTimeKind.Utc);

        // Act
        var device = Device.Create(userId, deviceId, platform, token, registeredAt);

        // Assert
        device.UserId.Should().Be(userId);
        device.DeviceId.Should().Be(deviceId);
        device.Platform.Should().Be(platform);
        device.Token.Should().Be(token);
        device.IsActive.Should().BeTrue();
        device.RegisteredAtUtc.Should().Be(registeredAt);
        device.Id.Should().NotBe(Guid.Empty);
    }

    [Theory]
    [InlineData("Android")]
    [InlineData("iOS")]
    [InlineData("Web")]
    public void Create_WithValidPlatforms_ShouldSucceed(string platform)
    {
        // Act
        var device = Device.Create(Guid.NewGuid(), "device-1", platform, "token-12345678901234567890", DateTime.UtcNow);

        // Assert
        device.Platform.Should().Be(platform);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithEmptyDeviceId_ShouldThrow(string invalidDeviceId)
    {
        // Act
        var act = () => Device.Create(Guid.NewGuid(), invalidDeviceId, "Android", "token-12345678901234567890", DateTime.UtcNow);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithEmptyPlatform_ShouldThrow(string invalidPlatform)
    {
        // Act
        var act = () => Device.Create(Guid.NewGuid(), "device-1", invalidPlatform, "token-12345678901234567890", DateTime.UtcNow);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithEmptyToken_ShouldThrow(string invalidToken)
    {
        // Act
        var act = () => Device.Create(Guid.NewGuid(), "device-1", "Android", invalidToken, DateTime.UtcNow);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void UpdateToken_WithValidToken_ShouldUpdateTokenAndMarkUsed()
    {
        // Arrange
        var device = Device.Create(Guid.NewGuid(), "device-1", "Android", "old-token-123456789012345678", DateTime.UtcNow);
        var newToken = "new-token-98765432109876543210";

        // Act
        device.UpdateToken(newToken);

        // Assert
        device.Token.Should().Be(newToken);
        device.LastUsedAtUtc.Should().NotBeNull();
        device.LastUsedAtUtc.Value.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void UpdateToken_WithEmptyToken_ShouldThrow(string invalidToken)
    {
        // Arrange
        var device = Device.Create(Guid.NewGuid(), "device-1", "Android", "token-12345678901234567890", DateTime.UtcNow);

        // Act
        var act = () => device.UpdateToken(invalidToken);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void MarkUsed_ShouldSetLastUsedAtUtc()
    {
        // Arrange
        var device = Device.Create(Guid.NewGuid(), "device-1", "Android", "token-12345678901234567890", DateTime.UtcNow);
        device.LastUsedAtUtc.Should().BeNull();

        // Act
        device.MarkUsed();

        // Assert
        device.LastUsedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public void Deactivate_ShouldSetIsActiveToFalse()
    {
        // Arrange
        var device = Device.Create(Guid.NewGuid(), "device-1", "Android", "token-12345678901234567890", DateTime.UtcNow);

        // Act
        device.Deactivate();

        // Assert
        device.IsActive.Should().BeFalse();
    }

    [Fact]
    public void Activate_ShouldSetIsActiveToTrue()
    {
        // Arrange
        var device = Device.Create(Guid.NewGuid(), "device-1", "Android", "token-12345678901234567890", DateTime.UtcNow);
        device.Deactivate();

        // Act
        device.Activate();

        // Assert
        device.IsActive.Should().BeTrue();
    }
}
