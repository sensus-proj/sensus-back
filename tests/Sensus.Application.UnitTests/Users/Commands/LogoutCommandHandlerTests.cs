using FluentAssertions;
using NSubstitute;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Users.Commands.Logout;
using Sensus.Domain.Entities;
using Sensus.Domain.Repositories;

namespace Sensus.Application.UnitTests.Users.Commands;

public class LogoutCommandHandlerTests
{
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly IDeviceRepository _deviceRepository = Substitute.For<IDeviceRepository>();
    private readonly LogoutCommandHandler _handler;

    public LogoutCommandHandlerTests()
    {
        _handler = new LogoutCommandHandler(_userRepository, _currentUserService, _deviceRepository);
    }

    [Fact]
    public async Task Handle_ShouldRotateSecurityStampAndUpdateUser_WhenUserExists()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _currentUserService.UserId.Returns(userId.ToString());

        var user = User.Create(
            "Test Logout User",
            "logout@sensus.com",
            "hashed-password",
            true,
            DateTime.UtcNow).Value;

        var originalStamp = user.SecurityStamp;

        _userRepository.GetByIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(user);

        // Act
        var result = await _handler.Handle(new LogoutCommand(), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        user.SecurityStamp.Should().NotBe(originalStamp);
        await _userRepository.Received(1).UpdateAsync(user, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldDeactivateOnlyTheRequestedDeviceOfTheAuthenticatedAccount()
    {
        User user = User.Create("Test Logout User", "logout@sensus.com", "hashed-password", true, DateTime.UtcNow).Value;
        Device device = Device.Create(user.Id, "installation", "Android", "test-device-token", DateTime.UtcNow);
        _currentUserService.UserId.Returns(user.Id.ToString());
        _userRepository.GetByIdAsync(user.Id, CancellationToken.None).Returns(user);
        _deviceRepository.GetByUserAndDeviceIdAsync(user.Id, device.DeviceId, CancellationToken.None).Returns(device);

        var result = await _handler.Handle(new LogoutCommand(device.DeviceId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await _deviceRepository.Received(1).DeactivateByTokenAsync(device.Id, device.Token, CancellationToken.None);
    }

    [Fact]
    public async Task Handle_ShouldRejectInvalidDeviceIdBeforeChangingTheSession()
    {
        _currentUserService.UserId.Returns(Guid.NewGuid().ToString());

        var result = await _handler.Handle(new LogoutCommand(" "), CancellationToken.None);

        result.Error.Code.Should().Be("Device.InvalidDeviceId");
        await _userRepository.DidNotReceive().UpdateAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenCallerIsNotAuthorized()
    {
        // Arrange
        _currentUserService.UserId.Returns((string?)null);

        // Act
        var result = await _handler.Handle(new LogoutCommand(), CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Auth.Unauthorized");
        await _userRepository.DidNotReceive().UpdateAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldReturnSuccess_WhenUserNotFound_ForIdempotency()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _currentUserService.UserId.Returns(userId.ToString());
        _userRepository.GetByIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns((User?)null);

        // Act
        var result = await _handler.Handle(new LogoutCommand(), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await _userRepository.DidNotReceive().UpdateAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
    }
}
