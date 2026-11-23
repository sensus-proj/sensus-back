using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Users.Commands.NotificationLocation;
using Sensus.Domain.Common;
using Sensus.Domain.Entities;
using Sensus.Domain.Repositories;
using Xunit;

namespace Sensus.Application.UnitTests.Users.Commands.NotificationLocation;

public sealed class NotificationLocationCommandsTests
{
    private static readonly DateTime Now = new(2026, 11, 22, 10, 0, 0, DateTimeKind.Utc);

    private readonly ICurrentUserService _currentUser = Substitute.For<ICurrentUserService>();
    private readonly IDateTimeProvider _clock = Substitute.For<IDateTimeProvider>();
    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly INotificationLocationRepository _locations = Substitute.For<INotificationLocationRepository>();
    private readonly IOptions<NotificationLocationSettings> _settings = Substitute.For<IOptions<NotificationLocationSettings>>();

    private readonly Guid _userId = Guid.NewGuid();
    private readonly ShareNotificationLocationCommandHandler _shareHandler;
    private readonly DeleteNotificationLocationCommandHandler _deleteHandler;

    public NotificationLocationCommandsTests()
    {
        _clock.UtcNow.Returns(Now);
        _currentUser.IsGuest.Returns(false);
        _currentUser.UserId.Returns(_userId.ToString());
        _settings.Value.Returns(new NotificationLocationSettings { LifetimeHours = 24 });

        _shareHandler = new ShareNotificationLocationCommandHandler(
            _currentUser,
            _clock,
            _users,
            _locations,
            _settings);

        _deleteHandler = new DeleteNotificationLocationCommandHandler(
            _currentUser,
            _locations);
    }

    [Fact]
    public async Task ShareLocation_WhenValid_SavesLocationSuccessfully()
    {
        var user = User.Create("Juan", "juan@test.com", "hash", true, Now).Value;
        _users.GetByIdAsync(_userId, Arg.Any<CancellationToken>()).Returns(user);

        var command = new ShareNotificationLocationCommand(19.4326, -99.1332);
        Result result = await _shareHandler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await _locations.Received(1).SaveAsync(
            _userId,
            19.4326,
            -99.1332,
            Now,
            Now.AddHours(24),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ShareLocation_WhenCallerIsGuest_ReturnsUnauthorized()
    {
        _currentUser.IsGuest.Returns(true);

        var command = new ShareNotificationLocationCommand(19.4, -99.1);
        Result result = await _shareHandler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Location.Unauthorized");
    }

    [Theory]
    [InlineData(null, -99.1)]
    [InlineData(19.4, null)]
    [InlineData(91.0, -99.1)]
    [InlineData(-91.0, -99.1)]
    [InlineData(19.4, 181.0)]
    [InlineData(19.4, -181.0)]
    [InlineData(double.NaN, -99.1)]
    [InlineData(19.4, double.PositiveInfinity)]
    public async Task ShareLocation_WhenCoordinatesInvalid_ReturnsValidationError(double? lat, double? lng)
    {
        var command = new ShareNotificationLocationCommand(lat, lng);
        Result result = await _shareHandler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Location.InvalidCoordinates");
    }

    [Fact]
    public async Task ShareLocation_WhenUserNotFoundInDatabase_DeletesSavedLocationAndReturnsUnauthorized()
    {
        _users.GetByIdAsync(_userId, Arg.Any<CancellationToken>()).Returns((User?)null);

        var command = new ShareNotificationLocationCommand(19.4, -99.1);
        Result result = await _shareHandler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Location.Unauthorized");
        await _locations.Received(1).DeleteAsync(_userId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteLocation_WhenValid_DeletesLocationSuccessfully()
    {
        var command = new DeleteNotificationLocationCommand();
        Result result = await _deleteHandler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await _locations.Received(1).DeleteAsync(_userId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteLocation_WhenCallerIsGuest_ReturnsUnauthorized()
    {
        _currentUser.IsGuest.Returns(true);

        var command = new DeleteNotificationLocationCommand();
        Result result = await _deleteHandler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Location.Unauthorized");
    }
}
