using FluentAssertions;
using NSubstitute;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Users.DTOs;
using Sensus.Application.Users.Queries.GetNotificationSettings;
using Sensus.Domain.Common;
using Sensus.Domain.Entities;
using Sensus.Domain.Repositories;
using Xunit;

namespace Sensus.Application.UnitTests.Users.Queries.GetNotificationSettings;

public sealed class GetNotificationSettingsQueryHandlerTests
{
    private readonly INotificationSettingsRepository _settingsRepository = Substitute.For<INotificationSettingsRepository>();
    private readonly ICurrentUserService _currentUser = Substitute.For<ICurrentUserService>();

    private readonly GetNotificationSettingsQueryHandler _handler;

    public GetNotificationSettingsQueryHandlerTests()
    {
        _handler = new GetNotificationSettingsQueryHandler(_settingsRepository, _currentUser);
    }

    [Fact]
    public async Task Handle_WhenUserNotAuthenticated_ShouldReturnUnauthorized()
    {
        // Arrange
        _currentUser.UserId.Returns((string?)null);

        // Act
        var result = await _handler.Handle(new GetNotificationSettingsQuery(), CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Settings.Unauthenticated");
    }

    [Fact]
    public async Task Handle_WhenSettingsDoNotExist_ShouldReturnDefaults()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _currentUser.UserId.Returns(userId.ToString());
        _settingsRepository.GetByUserIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns((NotificationSettings?)null);

        // Act
        var result = await _handler.Handle(new GetNotificationSettingsQuery(), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.UserId.Should().Be(userId);
        result.Value.PushEnabled.Should().BeTrue();
        result.Value.OfferAlerts.Should().BeTrue();
        result.Value.SocialAlerts.Should().BeTrue();
        result.Value.RadiusKm.Should().Be(5);
        result.Value.UpdatedAtUtc.Should().BeNull();
        await _settingsRepository.Received(1).AddAsync(Arg.Any<NotificationSettings>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenSettingsExist_ShouldReturnSettings()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _currentUser.UserId.Returns(userId.ToString());
        var existingSettings = NotificationSettings.CreateDefaults(userId);
        existingSettings.Update(false, true, false, 15);
        _settingsRepository.GetByUserIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(existingSettings);

        // Act
        var result = await _handler.Handle(new GetNotificationSettingsQuery(), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.UserId.Should().Be(userId);
        result.Value.PushEnabled.Should().BeFalse();
        result.Value.OfferAlerts.Should().BeTrue();
        result.Value.SocialAlerts.Should().BeFalse();
        result.Value.RadiusKm.Should().Be(15);
        result.Value.UpdatedAtUtc.Should().NotBeNull();
    }
}
