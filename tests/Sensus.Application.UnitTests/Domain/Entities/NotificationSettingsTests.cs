using FluentAssertions;
using Sensus.Domain.Entities;
using Xunit;

namespace Sensus.Application.UnitTests.Domain.Entities;

public sealed class NotificationSettingsTests
{
    [Fact]
    public void CreateDefaults_ShouldReturnDefaults()
    {
        // Arrange
        var userId = Guid.NewGuid();

        // Act
        var settings = NotificationSettings.CreateDefaults(userId);

        // Assert
        settings.UserId.Should().Be(userId);
        settings.PushEnabled.Should().BeTrue();
        settings.OfferAlerts.Should().BeTrue();
        settings.SocialAlerts.Should().BeTrue();
        settings.RadiusKm.Should().Be(5);
        settings.CreatedAtUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        settings.UpdatedAtUtc.Should().BeNull();
        settings.Id.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void Update_WithAllValues_ShouldUpdateAll()
    {
        // Arrange
        var settings = NotificationSettings.CreateDefaults(Guid.NewGuid());

        // Act
        settings.Update(false, false, false, 10);

        // Assert
        settings.PushEnabled.Should().BeFalse();
        settings.OfferAlerts.Should().BeFalse();
        settings.SocialAlerts.Should().BeFalse();
        settings.RadiusKm.Should().Be(10);
        settings.UpdatedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public void Update_WithNullValues_ShouldKeepExistingValues()
    {
        // Arrange
        var settings = NotificationSettings.CreateDefaults(Guid.NewGuid());

        // Act
        settings.Update(pushEnabled: null, offerAlerts: null, socialAlerts: null, radiusKm: null);

        // Assert
        settings.PushEnabled.Should().BeTrue();
        settings.OfferAlerts.Should().BeTrue();
        settings.SocialAlerts.Should().BeTrue();
        settings.RadiusKm.Should().Be(5);
    }

    [Fact]
    public void Update_WithPartialValues_ShouldUpdateOnlyProvided()
    {
        // Arrange
        var settings = NotificationSettings.CreateDefaults(Guid.NewGuid());

        // Act
        settings.Update(pushEnabled: false, offerAlerts: null, socialAlerts: null, radiusKm: 25);

        // Assert
        settings.PushEnabled.Should().BeFalse();
        settings.OfferAlerts.Should().BeTrue();
        settings.SocialAlerts.Should().BeTrue();
        settings.RadiusKm.Should().Be(25);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(51)]
    [InlineData(100)]
    public void Update_WithRadiusKmOutOfRange_ShouldThrow(int invalidRadiusKm)
    {
        // Arrange
        var settings = NotificationSettings.CreateDefaults(Guid.NewGuid());

        // Act
        var act = () => settings.Update(pushEnabled: null, offerAlerts: null, socialAlerts: null, radiusKm: invalidRadiusKm);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(50)]
    public void Update_WithRadiusKmAtBoundaries_ShouldSucceed(int validRadiusKm)
    {
        // Arrange
        var settings = NotificationSettings.CreateDefaults(Guid.NewGuid());

        // Act
        settings.Update(pushEnabled: null, offerAlerts: null, socialAlerts: null, radiusKm: validRadiusKm);

        // Assert
        settings.RadiusKm.Should().Be(validRadiusKm);
    }
}
