using FluentAssertions;
using NSubstitute;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Users.Commands.RegisterDevice;
using Sensus.Application.Users.DTOs;
using Sensus.Domain.Common;
using Sensus.Domain.Entities;
using Sensus.Domain.Repositories;
using Xunit;

namespace Sensus.Application.UnitTests.Users.Commands.RegisterDevice;

public sealed class RegisterDeviceCommandHandlerTests
{
    private readonly IDeviceRepository _deviceRepository = Substitute.For<IDeviceRepository>();
    private readonly IPushDispatcher _pushDispatcher = Substitute.For<IPushDispatcher>();
    private readonly ICurrentUserService _currentUser = Substitute.For<ICurrentUserService>();
    private readonly IDateTimeProvider _dateTimeProvider = Substitute.For<IDateTimeProvider>();

    private readonly Guid _userId = Guid.NewGuid();
    private readonly RegisterDeviceCommandHandler _handler;

    public RegisterDeviceCommandHandlerTests()
    {
        _handler = new RegisterDeviceCommandHandler(
            _deviceRepository,
            _pushDispatcher,
            _currentUser,
            _dateTimeProvider);
    }

    [Fact]
    public async Task Handle_WhenUserNotAuthenticated_ShouldReturnUnauthorized()
    {
        // Arrange
        _currentUser.UserId.Returns((string?)null);
        var command = new RegisterDeviceCommand("device-1", "Android", "token-12345678901234567890");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Device.Unauthenticated");
    }

    [Fact]
    public async Task Handle_WhenTokenInvalid_ShouldReturnValidationError()
    {
        // Arrange
        _currentUser.UserId.Returns(_userId.ToString());
        _pushDispatcher.ValidateTokenAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);
        var command = new RegisterDeviceCommand("device-1", "Android", "invalid-token");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Device.InvalidToken");
    }

    [Fact]
    public async Task Handle_WhenDeviceAlreadyExists_ShouldUpdateToken()
    {
        // Arrange
        _currentUser.UserId.Returns(_userId.ToString());
        _pushDispatcher.ValidateTokenAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
        var existingDevice = Device.Create(_userId, "device-1", "Android", "old-token-12345678901234567890", DateTime.UtcNow);
        _deviceRepository.GetByUserAndDeviceIdAsync(_userId, "device-1", Arg.Any<CancellationToken>())
            .Returns(existingDevice);
        var command = new RegisterDeviceCommand("device-1", "Android", "new-token-12345678901234567890");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Token.Should().Be("new-token-12345678901234567890");
        result.Value.IsActive.Should().BeTrue();
        await _deviceRepository.Received(1).UpdateAsync(Arg.Any<Device>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenNewDevice_ShouldCreateAndReturnSuccess()
    {
        // Arrange
        _currentUser.UserId.Returns(_userId.ToString());
        _pushDispatcher.ValidateTokenAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
        _deviceRepository.GetByUserAndDeviceIdAsync(_userId, "device-1", Arg.Any<CancellationToken>())
            .Returns((Device?)null);
        _deviceRepository.GetByTokenAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((Device?)null);
        var command = new RegisterDeviceCommand("device-1", "Android", "token-12345678901234567890");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.DeviceId.Should().Be("device-1");
        result.Value.Platform.Should().Be("Android");
        result.Value.Token.Should().Be("token-12345678901234567890");
        result.Value.IsActive.Should().BeTrue();
        await _deviceRepository.Received(1).AddAsync(Arg.Any<Device>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenTokenBelongsToAnotherUser_ShouldReassign()
    {
        // Arrange
        var otherUserId = Guid.NewGuid();
        _currentUser.UserId.Returns(_userId.ToString());
        _pushDispatcher.ValidateTokenAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
        _deviceRepository.GetByUserAndDeviceIdAsync(_userId, "device-1", Arg.Any<CancellationToken>())
            .Returns((Device?)null);
        var otherUserDevice = Device.Create(otherUserId, "device-2", "iOS", "token-12345678901234567890", DateTime.UtcNow);
        _deviceRepository.GetByTokenAsync("token-12345678901234567890", Arg.Any<CancellationToken>())
            .Returns(otherUserDevice);
        var command = new RegisterDeviceCommand("device-1", "Android", "token-12345678901234567890");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        // Verificar que se desactivó el dispositivo del otro usuario comprobando el estado
        otherUserDevice.IsActive.Should().BeFalse();
        await _deviceRepository.Received(1).UpdateAsync(otherUserDevice, Arg.Any<CancellationToken>());
        await _deviceRepository.Received(1).AddAsync(Arg.Any<Device>(), Arg.Any<CancellationToken>());
    }
}
