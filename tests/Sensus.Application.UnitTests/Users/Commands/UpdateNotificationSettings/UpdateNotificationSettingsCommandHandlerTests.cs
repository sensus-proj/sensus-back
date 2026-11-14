using FluentAssertions;
using NSubstitute;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Users.Commands.UpdateNotificationSettings;
using Sensus.Application.Users.DTOs;
using Sensus.Domain.Common;
using Sensus.Domain.Entities;
using Sensus.Domain.Repositories;
using Xunit;

namespace Sensus.Application.UnitTests.Users.Commands.UpdateNotificationSettings;

public sealed class UpdateNotificationSettingsCommandHandlerTests
{
    private readonly INotificationSettingsRepository _settingsRepository = Substitute.For<INotificationSettingsRepository>();
    private readonly ICurrentUserService _currentUser = Substitute.For<ICurrentUserService>();

    private readonly UpdateNotificationSettingsCommandHandler _handler;

    public UpdateNotificationSettingsCommandHandlerTests()
    {
        _handler = new UpdateNotificationSettingsCommandHandler(_settingsRepository, _currentUser);
    }

    [Fact]
    public async Task Handle_WhenUserNotAuthenticated_ShouldReturnUnauthorized()
    {
        // Arrange
        _currentUser.UserId.Returns((string?)null);
        var command = new UpdateNotificationSettingsCommand(true, true, true, 10);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Settings.Unauthenticated");
    }

    [Fact]
    public async Task Handle_WhenSettingsDoNotExist_ShouldCreateWithDefaultsAndUpdate()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _currentUser.UserId.Returns(userId.ToString());
        _settingsRepository.GetByUserIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns((NotificationSettings?)null);
        var command = new UpdateNotificationSettingsCommand(false, false, false, 25);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.PushEnabled.Should().BeFalse();
        result.Value.OfferAlerts.Should().BeFalse();
        result.Value.SocialAlerts.Should().BeFalse();
        result.Value.RadiusKm.Should().Be(25);
        await _settingsRepository.Received(1).AddAsync(Arg.Any<NotificationSettings>(), Arg.Any<CancellationToken>());
        await _settingsRepository.Received(1).UpdateAsync(Arg.Any<NotificationSettings>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenSettingsExist_ShouldUpdateAndReturn()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _currentUser.UserId.Returns(userId.ToString());
        var existingSettings = NotificationSettings.CreateDefaults(userId);
        _settingsRepository.GetByUserIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(existingSettings);
        var command = new UpdateNotificationSettingsCommand(false, true, false, 10);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.PushEnabled.Should().BeFalse();
        result.Value.OfferAlerts.Should().BeTrue();
        result.Value.SocialAlerts.Should().BeFalse();
        result.Value.RadiusKm.Should().Be(10);
        await _settingsRepository.Received(1).UpdateAsync(Arg.Any<NotificationSettings>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(51)]
    [InlineData(-1)]
    public async Task Handle_WhenRadiusKmOutOfRange_ShouldReturnValidationError(int invalidRadiusKm)
    {
        // Arrange
        var userId = Guid.NewGuid();
        _currentUser.UserId.Returns(userId.ToString());
        _settingsRepository.GetByUserIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns((NotificationSettings?)null);
        var command = new UpdateNotificationSettingsCommand(null, null, null, invalidRadiusKm);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Settings.InvalidRadius");
    }
}
