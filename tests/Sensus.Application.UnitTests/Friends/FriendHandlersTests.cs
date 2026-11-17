using FluentAssertions;
using NSubstitute;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Friends.Commands.AcceptFriendRequest;
using Sensus.Application.Friends.Commands.RejectFriendRequest;
using Sensus.Application.Friends.Commands.RemoveFriend;
using Sensus.Application.Friends.Commands.SendFriendRequest;
using Sensus.Application.Friends.Queries.GetFriendsOverview;
using Sensus.Domain.Common;
using Sensus.Domain.Entities;
using Sensus.Domain.Enums;
using Sensus.Domain.Repositories;
using Xunit;

namespace Sensus.Application.UnitTests.Friends;

public sealed class FriendHandlersTests
{
    private static readonly DateTime FixedNow = new(2026, 11, 16, 12, 0, 0, DateTimeKind.Utc);
    private readonly ICurrentUserService _currentUser = Substitute.For<ICurrentUserService>();
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IFriendRequestRepository _friendRequestRepository = Substitute.For<IFriendRequestRepository>();
    private readonly IDeviceRepository _deviceRepository = Substitute.For<IDeviceRepository>();
    private readonly IPushDispatcher _pushDispatcher = Substitute.For<IPushDispatcher>();
    private readonly IDateTimeProvider _dateTimeProvider = Substitute.For<IDateTimeProvider>();

    public FriendHandlersTests()
    {
        _dateTimeProvider.UtcNow.Returns(FixedNow);
        _pushDispatcher.SendAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<IReadOnlyDictionary<string, string>?>(),
            Arg.Any<CancellationToken>()).Returns(true);
    }

    [Fact]
    public void FriendInviteCodeGenerator_ProducesDeterministicUppercaseCode()
    {
        var code1 = FriendInviteCodeGenerator.Generate(" Alberto@Sensus.com ");
        var code2 = FriendInviteCodeGenerator.Generate("alberto@sensus.com");

        code1.Should().Be(code2);
        code1.Should().StartWith("SNS-").And.HaveLength(10);
        FriendInviteCodeGenerator.Generate(null, null).Should().Be("SNS-VECINO");
    }

    [Fact]
    public async Task SendFriendRequest_CreatesPendingRequestAndNotifiesTargetDevices()
    {
        var sender = User.Create("Alberto", "alberto@sensus.com", "hash", true, FixedNow).Value;
        var target = User.Create("Maria", "maria@sensus.com", "hash", true, FixedNow).Value;
        var targetCode = FriendInviteCodeGenerator.Generate(target.Correo);
        var device = Device.Create(target.Id, "dev-1", "Android", "fcm-token-12345678901234567890", FixedNow);

        _currentUser.UserId.Returns(sender.Id.ToString());
        _currentUser.IsGuest.Returns(false);
        _userRepository.GetByIdAsync(sender.Id, Arg.Any<CancellationToken>()).Returns(sender);
        _userRepository.GetByInviteCodeAsync(targetCode, Arg.Any<CancellationToken>()).Returns(target);
        _friendRequestRepository.GetBetweenUsersAsync(sender.Id, target.Id, Arg.Any<CancellationToken>()).Returns((FriendRequest?)null);
        _deviceRepository.GetActiveByUserAsync(target.Id, Arg.Any<CancellationToken>()).Returns([device]);

        var handler = new SendFriendRequestCommandHandler(
            _currentUser, _userRepository, _friendRequestRepository, _deviceRepository, _pushDispatcher, _dateTimeProvider);

        var result = await handler.Handle(new SendFriendRequestCommand(targetCode, "QR"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Outcome.Should().Be("Sent");
        result.Value.PendingRequest.Should().NotBeNull();
        result.Value.PendingRequest!.TargetInviteCode.Should().Be(targetCode);
        await _friendRequestRepository.Received(1).AddAsync(Arg.Any<FriendRequest>(), Arg.Any<CancellationToken>());
        await _pushDispatcher.Received(1).SendAsync(
            device.Token,
            "Nueva solicitud de amistad",
            Arg.Is<string>(b => b.Contains("Alberto")),
            Arg.Any<IReadOnlyDictionary<string, string>?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendFriendRequest_WhenMutualPendingExists_AcceptsAutomatically()
    {
        var sender = User.Create("Alberto", "alberto@sensus.com", "hash", true, FixedNow).Value;
        var target = User.Create("Maria", "maria@sensus.com", "hash", true, FixedNow).Value;
        var senderCode = FriendInviteCodeGenerator.Generate(sender.Correo);
        var targetCode = FriendInviteCodeGenerator.Generate(target.Correo);
        var reversePending = FriendRequest.Create(
            target.Id, target.NombreCompleto, targetCode,
            sender.Id, sender.NombreCompleto, senderCode,
            "QR", FixedNow.AddMinutes(-5)).Value;

        _currentUser.UserId.Returns(sender.Id.ToString());
        _userRepository.GetByIdAsync(sender.Id, Arg.Any<CancellationToken>()).Returns(sender);
        _userRepository.GetByInviteCodeAsync(targetCode, Arg.Any<CancellationToken>()).Returns(target);
        _friendRequestRepository.GetBetweenUsersAsync(sender.Id, target.Id, Arg.Any<CancellationToken>()).Returns(reversePending);
        _deviceRepository.GetActiveByUserAsync(target.Id, Arg.Any<CancellationToken>()).Returns([]);

        var handler = new SendFriendRequestCommandHandler(
            _currentUser, _userRepository, _friendRequestRepository, _deviceRepository, _pushDispatcher, _dateTimeProvider);

        var result = await handler.Handle(new SendFriendRequestCommand(targetCode, "QR"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Outcome.Should().Be("AcceptedMutual");
        result.Value.Friend.Should().NotBeNull();
        result.Value.Friend!.DisplayName.Should().Be("Maria");
        reversePending.Status.Should().Be(FriendRequestStatus.Accepted);
    }

    [Fact]
    public async Task AcceptAndRejectFriendRequest_RespectTargetOwnership()
    {
        var senderId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        var request = FriendRequest.Create(senderId, "Carlos", "SNS-111111", targetId, "Lucia", "SNS-222222", "QR", FixedNow).Value;
        var device = Device.Create(senderId, "dev-s", "Android", "token-12345678901234567890", FixedNow);

        _currentUser.UserId.Returns(senderId.ToString());
        _friendRequestRepository.GetByIdAsync(request.Id, Arg.Any<CancellationToken>()).Returns(request);

        var acceptHandler = new AcceptFriendRequestCommandHandler(
            _currentUser, _friendRequestRepository, _deviceRepository, _pushDispatcher, _dateTimeProvider);

        var forbiddenResult = await acceptHandler.Handle(new AcceptFriendRequestCommand(request.Id), CancellationToken.None);
        forbiddenResult.IsFailure.Should().BeTrue();
        forbiddenResult.Error.Type.Should().Be(ErrorType.Forbidden);

        _currentUser.UserId.Returns(targetId.ToString());
        _deviceRepository.GetActiveByUserAsync(senderId, Arg.Any<CancellationToken>()).Returns([device]);

        var accepted = await acceptHandler.Handle(new AcceptFriendRequestCommand(request.Id), CancellationToken.None);
        accepted.IsSuccess.Should().BeTrue();
        accepted.Value.DisplayName.Should().Be("Carlos");
        request.Status.Should().Be(FriendRequestStatus.Accepted);

        var rejectHandler = new RejectFriendRequestCommandHandler(_currentUser, _friendRequestRepository, _dateTimeProvider);
        var rejected = await rejectHandler.Handle(new RejectFriendRequestCommand(request.Id), CancellationToken.None);
        rejected.IsSuccess.Should().BeTrue();
        request.Status.Should().Be(FriendRequestStatus.Rejected);
    }

    [Fact]
    public async Task GetFriendsOverviewAndRemoveFriend_ReturnsGroupedListsAndRemovesConnection()
    {
        var user = User.Create("Lucia", "lucia@sensus.com", "hash", true, FixedNow).Value;
        var friendId = Guid.NewGuid();
        var otherId = Guid.NewGuid();

        var accepted = FriendRequest.Create(friendId, "Ana", "SNS-AAAAAA", user.Id, user.NombreCompleto, "SNS-BBBBBB", "QR", FixedNow).Value;
        accepted.Accept(FixedNow.AddMinutes(1));
        var pendingIn = FriendRequest.Create(otherId, "Luis", "SNS-CCCCCC", user.Id, user.NombreCompleto, "SNS-BBBBBB", "Enlace", FixedNow).Value;

        _currentUser.UserId.Returns(user.Id.ToString());
        _userRepository.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);
        _friendRequestRepository.GetForUserAsync(user.Id, Arg.Any<CancellationToken>()).Returns([accepted, pendingIn]);

        var overviewHandler = new GetFriendsOverviewQueryHandler(_currentUser, _userRepository, _friendRequestRepository);
        var overview = await overviewHandler.Handle(new GetFriendsOverviewQuery(), CancellationToken.None);

        overview.IsSuccess.Should().BeTrue();
        overview.Value.Friends.Should().ContainSingle(f => f.DisplayName == "Ana");
        overview.Value.PendingReceived.Should().ContainSingle(p => p.SenderName == "Luis");
        overview.Value.PendingSent.Should().BeEmpty();

        _friendRequestRepository.GetBetweenUsersAsync(user.Id, friendId, Arg.Any<CancellationToken>()).Returns(accepted);
        var removeHandler = new RemoveFriendCommandHandler(_currentUser, _friendRequestRepository);
        var removeResult = await removeHandler.Handle(new RemoveFriendCommand(friendId), CancellationToken.None);

        removeResult.IsSuccess.Should().BeTrue();
        await _friendRequestRepository.Received(1).DeleteAsync(accepted.Id, Arg.Any<CancellationToken>());
    }
}
