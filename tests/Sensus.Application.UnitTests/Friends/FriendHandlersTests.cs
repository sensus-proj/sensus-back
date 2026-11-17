using FluentAssertions;
using NSubstitute;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Friends.Commands.AcceptFriendRequest;
using Sensus.Application.Friends.Commands.RejectFriendRequest;
using Sensus.Application.Friends.Commands.RemoveFriend;
using Sensus.Application.Friends.Commands.SendFriendRequest;
using Sensus.Application.Friends.DTOs;
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
    public void FriendInviteCodeGenerator_And_EntityValidations_CoverBranches()
    {
        string code1 = FriendInviteCodeGenerator.Generate(" Alberto@Sensus.com ");
        string code2 = FriendInviteCodeGenerator.Generate("alberto@sensus.com");
        string nameFallback = FriendInviteCodeGenerator.Generate(null, "Alberto");

        code1.Should().Be(code2);
        code1.Should().StartWith("SNS-").And.HaveLength(10);
        nameFallback.Should().StartWith("SNS-");
        FriendInviteCodeGenerator.Generate(null, null).Should().Be("SNS-VECINO");

        Guid senderId = Guid.NewGuid();
        Guid targetId = Guid.NewGuid();

        FriendRequest.Create(Guid.Empty, "A", "SNS-1", targetId, "B", "SNS-2", "QR", FixedNow).IsFailure.Should().BeTrue();
        FriendRequest.Create(senderId, "A", "SNS-1", senderId, "A", "SNS-1", "QR", FixedNow).IsFailure.Should().BeTrue();
        FriendRequest.Create(senderId, "A", "", targetId, "B", "SNS-2", "QR", FixedNow).IsFailure.Should().BeTrue();

        FriendRequest created = FriendRequest.Create(senderId, " ", "sns-111111", targetId, " ", "sns-222222", " ", FixedNow).Value;
        created.SenderName.Should().Be("sns-111111");
        created.TargetName.Should().Be("sns-222222");
        created.SenderInviteCode.Should().Be("SNS-111111");
        created.TargetInviteCode.Should().Be("SNS-222222");
        created.AddedVia.Should().Be("QR");

        FriendRequest rehydrated = FriendRequest.Rehydrate(
            created.Id, senderId, "A", "SNS-111111", targetId, "B", "SNS-222222", "Enlace", FriendRequestStatus.Pending, FixedNow, null);
        rehydrated.Accept(FixedNow.AddMinutes(1)).IsSuccess.Should().BeTrue();
        rehydrated.Accept(FixedNow.AddMinutes(2)).IsSuccess.Should().BeTrue();
        rehydrated.Reject(FixedNow.AddMinutes(3)).IsSuccess.Should().BeTrue();
        rehydrated.Reject(FixedNow.AddMinutes(4)).IsSuccess.Should().BeTrue();
        rehydrated.Accept(FixedNow.AddMinutes(5)).IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task SendFriendRequest_CreatesPendingRequestAndNotifiesTargetDevices()
    {
        User sender = User.Create("Alberto", "alberto@sensus.com", "hash", true, FixedNow).Value;
        User target = User.Create("Maria", "maria@sensus.com", "hash", true, FixedNow).Value;
        string targetCode = FriendInviteCodeGenerator.Generate(target.Correo);
        Device device = Device.Create(target.Id, "dev-1", "Android", "fcm-token-12345678901234567890", FixedNow);
        SendFriendRequestInput inputDto = new(targetCode, "QR");

        _currentUser.UserId.Returns(sender.Id.ToString());
        _currentUser.IsGuest.Returns(false);
        _userRepository.GetByIdAsync(sender.Id, Arg.Any<CancellationToken>()).Returns(sender);
        _userRepository.GetByInviteCodeAsync(targetCode, Arg.Any<CancellationToken>()).Returns(target);
        _friendRequestRepository.GetBetweenUsersAsync(sender.Id, target.Id, Arg.Any<CancellationToken>()).Returns((FriendRequest?)null);
        _deviceRepository.GetActiveByUserAsync(target.Id, Arg.Any<CancellationToken>()).Returns([device]);

        SendFriendRequestCommandHandler handler = new(
            _currentUser, _userRepository, _friendRequestRepository, _deviceRepository, _pushDispatcher, _dateTimeProvider);

        Result<SendFriendRequestResponseDto> result = await handler.Handle(
            new SendFriendRequestCommand(inputDto.InviteCode, inputDto.AddedVia), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Outcome.Should().Be("Sent");
        result.Value.Friend.Should().BeNull();
        result.Value.PendingRequest.Should().NotBeNull();
        result.Value.PendingRequest!.TargetInviteCode.Should().Be(targetCode);
        result.Value.PendingRequest.AddedVia.Should().Be("QR");
        result.Value.PendingRequest.CreatedAtUtc.Should().Be(FixedNow);
        await _friendRequestRepository.Received(1).AddAsync(Arg.Any<FriendRequest>(), Arg.Any<CancellationToken>());
        await _pushDispatcher.Received(1).SendAsync(
            device.Token,
            "Nueva solicitud de amistad",
            Arg.Is<string>(b => b.Contains("Alberto")),
            Arg.Any<IReadOnlyDictionary<string, string>?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendFriendRequest_CoversErrorAndIdempotencyBranches()
    {
        User sender = User.Create("Alberto", "alberto@sensus.com", "hash", true, FixedNow).Value;
        User target = User.Create("Maria", "maria@sensus.com", "hash", true, FixedNow).Value;
        string senderCode = FriendInviteCodeGenerator.Generate(sender.Correo);
        string targetCode = FriendInviteCodeGenerator.Generate(target.Correo);

        SendFriendRequestCommandHandler handler = new(
            _currentUser, _userRepository, _friendRequestRepository, _deviceRepository, _pushDispatcher, _dateTimeProvider);

        _currentUser.IsGuest.Returns(true);
        (await handler.Handle(new SendFriendRequestCommand(targetCode), CancellationToken.None)).Error.Type.Should().Be(ErrorType.Unauthorized);

        _currentUser.IsGuest.Returns(false);
        _currentUser.UserId.Returns(sender.Id.ToString());
        (await handler.Handle(new SendFriendRequestCommand("123"), CancellationToken.None)).Error.Type.Should().Be(ErrorType.Validation);

        _userRepository.GetByIdAsync(sender.Id, Arg.Any<CancellationToken>()).Returns((User?)null);
        (await handler.Handle(new SendFriendRequestCommand(targetCode), CancellationToken.None)).Error.Type.Should().Be(ErrorType.NotFound);

        _userRepository.GetByIdAsync(sender.Id, Arg.Any<CancellationToken>()).Returns(sender);
        _userRepository.GetByInviteCodeAsync(targetCode, Arg.Any<CancellationToken>()).Returns((User?)null);
        (await handler.Handle(new SendFriendRequestCommand(targetCode), CancellationToken.None)).Error.Type.Should().Be(ErrorType.NotFound);

        _userRepository.GetByInviteCodeAsync(senderCode, Arg.Any<CancellationToken>()).Returns(sender);
        (await handler.Handle(new SendFriendRequestCommand(senderCode), CancellationToken.None)).Error.Type.Should().Be(ErrorType.Validation);

        _userRepository.GetByInviteCodeAsync(targetCode, Arg.Any<CancellationToken>()).Returns(target);

        FriendRequest accepted = FriendRequest.Create(sender.Id, "Alberto", senderCode, target.Id, "Maria", targetCode, "QR", FixedNow).Value;
        accepted.Accept(FixedNow);
        _friendRequestRepository.GetBetweenUsersAsync(sender.Id, target.Id, Arg.Any<CancellationToken>()).Returns(accepted);
        Result<SendFriendRequestResponseDto> alreadyConnected = await handler.Handle(new SendFriendRequestCommand(targetCode), CancellationToken.None);
        alreadyConnected.Value.Outcome.Should().Be("AlreadyConnected");

        FriendRequest pendingBySender = FriendRequest.Create(sender.Id, "Alberto", senderCode, target.Id, "Maria", targetCode, "QR", FixedNow).Value;
        _friendRequestRepository.GetBetweenUsersAsync(sender.Id, target.Id, Arg.Any<CancellationToken>()).Returns(pendingBySender);
        Result<SendFriendRequestResponseDto> alreadyPending = await handler.Handle(new SendFriendRequestCommand(targetCode), CancellationToken.None);
        alreadyPending.Value.Outcome.Should().Be("AlreadyPending");

        FriendRequest rejectedOld = FriendRequest.Create(sender.Id, "Alberto", senderCode, target.Id, "Maria", targetCode, "QR", FixedNow).Value;
        rejectedOld.Reject(FixedNow);
        _friendRequestRepository.GetBetweenUsersAsync(sender.Id, target.Id, Arg.Any<CancellationToken>()).Returns(rejectedOld);
        _deviceRepository.GetActiveByUserAsync(target.Id, Arg.Any<CancellationToken>()).Returns([]);
        Result<SendFriendRequestResponseDto> resent = await handler.Handle(new SendFriendRequestCommand(targetCode, null), CancellationToken.None);
        resent.Value.Outcome.Should().Be("Sent");
        await _friendRequestRepository.Received(1).DeleteAsync(rejectedOld.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendFriendRequest_WhenMutualPendingExists_AcceptsAutomatically()
    {
        User sender = User.Create("Alberto", "alberto@sensus.com", "hash", true, FixedNow).Value;
        User target = User.Create("Maria", "maria@sensus.com", "hash", true, FixedNow).Value;
        string senderCode = FriendInviteCodeGenerator.Generate(sender.Correo);
        string targetCode = FriendInviteCodeGenerator.Generate(target.Correo);
        FriendRequest reversePending = FriendRequest.Create(
            target.Id, target.NombreCompleto, targetCode,
            sender.Id, sender.NombreCompleto, senderCode,
            "QR", FixedNow.AddMinutes(-5)).Value;

        _currentUser.UserId.Returns(sender.Id.ToString());
        _userRepository.GetByIdAsync(sender.Id, Arg.Any<CancellationToken>()).Returns(sender);
        _userRepository.GetByInviteCodeAsync(targetCode, Arg.Any<CancellationToken>()).Returns(target);
        _friendRequestRepository.GetBetweenUsersAsync(sender.Id, target.Id, Arg.Any<CancellationToken>()).Returns(reversePending);
        _deviceRepository.GetActiveByUserAsync(target.Id, Arg.Any<CancellationToken>()).Returns([]);

        SendFriendRequestCommandHandler handler = new(
            _currentUser, _userRepository, _friendRequestRepository, _deviceRepository, _pushDispatcher, _dateTimeProvider);

        Result<SendFriendRequestResponseDto> result = await handler.Handle(new SendFriendRequestCommand(targetCode, "QR"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Outcome.Should().Be("AcceptedMutual");
        result.Value.Friend.Should().NotBeNull();
        result.Value.Friend!.DisplayName.Should().Be("Maria");
        reversePending.Status.Should().Be(FriendRequestStatus.Accepted);
    }

    [Fact]
    public async Task AcceptAndRejectFriendRequest_CoverAuthNotFoundForbiddenAndConflictBranches()
    {
        Guid senderId = Guid.NewGuid();
        Guid targetId = Guid.NewGuid();
        Guid strangerId = Guid.NewGuid();
        FriendRequest request = FriendRequest.Create(senderId, "Carlos", "SNS-111111", targetId, "Lucia", "SNS-222222", "QR", FixedNow).Value;
        Device device = Device.Create(senderId, "dev-s", "Android", "token-12345678901234567890", FixedNow);

        AcceptFriendRequestCommandHandler acceptHandler = new(
            _currentUser, _friendRequestRepository, _deviceRepository, _pushDispatcher, _dateTimeProvider);
        RejectFriendRequestCommandHandler rejectHandler = new(
            _currentUser, _friendRequestRepository, _dateTimeProvider);

        _currentUser.UserId.Returns((string?)null);
        (await acceptHandler.Handle(new AcceptFriendRequestCommand(request.Id), CancellationToken.None)).Error.Type.Should().Be(ErrorType.Unauthorized);
        (await rejectHandler.Handle(new RejectFriendRequestCommand(request.Id), CancellationToken.None)).Error.Type.Should().Be(ErrorType.Unauthorized);

        _currentUser.UserId.Returns(targetId.ToString());
        _friendRequestRepository.GetByIdAsync(request.Id, Arg.Any<CancellationToken>()).Returns((FriendRequest?)null);
        (await acceptHandler.Handle(new AcceptFriendRequestCommand(request.Id), CancellationToken.None)).Error.Type.Should().Be(ErrorType.NotFound);
        (await rejectHandler.Handle(new RejectFriendRequestCommand(request.Id), CancellationToken.None)).Error.Type.Should().Be(ErrorType.NotFound);

        _friendRequestRepository.GetByIdAsync(request.Id, Arg.Any<CancellationToken>()).Returns(request);
        _currentUser.UserId.Returns(senderId.ToString());
        (await acceptHandler.Handle(new AcceptFriendRequestCommand(request.Id), CancellationToken.None)).Error.Type.Should().Be(ErrorType.Forbidden);

        _currentUser.UserId.Returns(strangerId.ToString());
        (await rejectHandler.Handle(new RejectFriendRequestCommand(request.Id), CancellationToken.None)).Error.Type.Should().Be(ErrorType.Forbidden);

        _currentUser.UserId.Returns(targetId.ToString());
        _deviceRepository.GetActiveByUserAsync(senderId, Arg.Any<CancellationToken>()).Returns([device]);

        Result<FriendItemDto> accepted = await acceptHandler.Handle(new AcceptFriendRequestCommand(request.Id), CancellationToken.None);
        accepted.IsSuccess.Should().BeTrue();
        accepted.Value.DisplayName.Should().Be("Carlos");
        accepted.Value.InviteCode.Should().Be("SNS-111111");
        accepted.Value.AddedVia.Should().Be("QR");

        Result rejected = await rejectHandler.Handle(new RejectFriendRequestCommand(request.Id), CancellationToken.None);
        rejected.IsSuccess.Should().BeTrue();
        request.Status.Should().Be(FriendRequestStatus.Rejected);

        (await acceptHandler.Handle(new AcceptFriendRequestCommand(request.Id), CancellationToken.None)).Error.Type.Should().Be(ErrorType.Conflict);
    }

    [Fact]
    public async Task GetFriendsOverviewAndRemoveFriend_CoverAllBranches()
    {
        User user = User.Create("Lucia", "lucia@sensus.com", "hash", true, FixedNow).Value;
        Guid friendId = Guid.NewGuid();
        Guid incomingId = Guid.NewGuid();
        Guid outgoingId = Guid.NewGuid();

        FriendRequest accepted = FriendRequest.Create(friendId, "Ana", "SNS-AAAAAA", user.Id, user.NombreCompleto, "SNS-BBBBBB", "QR", FixedNow).Value;
        accepted.Accept(FixedNow.AddMinutes(1));
        FriendRequest pendingIn = FriendRequest.Create(incomingId, "Luis", "SNS-CCCCCC", user.Id, user.NombreCompleto, "SNS-BBBBBB", "Enlace", FixedNow).Value;
        FriendRequest pendingOut = FriendRequest.Create(user.Id, user.NombreCompleto, "SNS-BBBBBB", outgoingId, "Sofia", "SNS-DDDDDD", "Código", FixedNow).Value;

        GetFriendsOverviewQueryHandler overviewHandler = new(_currentUser, _userRepository, _friendRequestRepository);
        RemoveFriendCommandHandler removeHandler = new(_currentUser, _friendRequestRepository);

        _currentUser.UserId.Returns("invalid-guid");
        (await overviewHandler.Handle(new GetFriendsOverviewQuery(), CancellationToken.None)).Error.Type.Should().Be(ErrorType.Unauthorized);
        (await removeHandler.Handle(new RemoveFriendCommand(friendId), CancellationToken.None)).Error.Type.Should().Be(ErrorType.Unauthorized);

        _currentUser.UserId.Returns(user.Id.ToString());
        _userRepository.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns((User?)null);
        (await overviewHandler.Handle(new GetFriendsOverviewQuery(), CancellationToken.None)).Error.Type.Should().Be(ErrorType.NotFound);

        _userRepository.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);
        _friendRequestRepository.GetForUserAsync(user.Id, Arg.Any<CancellationToken>()).Returns([accepted, pendingIn, pendingOut]);

        Result<FriendsOverviewDto> overview = await overviewHandler.Handle(new GetFriendsOverviewQuery(), CancellationToken.None);
        overview.IsSuccess.Should().BeTrue();
        overview.Value.MyInviteCode.Should().StartWith("SNS-");
        overview.Value.Friends.Should().ContainSingle(f => f.DisplayName == "Ana");
        overview.Value.PendingReceived.Should().ContainSingle(p => p.SenderName == "Luis" && p.AddedVia == "Enlace");
        overview.Value.PendingSent.Should().ContainSingle(s => s.TargetName == "Sofia");

        (await removeHandler.Handle(new RemoveFriendCommand(Guid.Empty), CancellationToken.None)).Error.Type.Should().Be(ErrorType.Validation);

        _friendRequestRepository.GetBetweenUsersAsync(user.Id, outgoingId, Arg.Any<CancellationToken>()).Returns((FriendRequest?)null);
        (await removeHandler.Handle(new RemoveFriendCommand(outgoingId), CancellationToken.None)).IsSuccess.Should().BeTrue();

        _friendRequestRepository.GetBetweenUsersAsync(user.Id, friendId, Arg.Any<CancellationToken>()).Returns(accepted);
        Result removeResult = await removeHandler.Handle(new RemoveFriendCommand(friendId), CancellationToken.None);
        removeResult.IsSuccess.Should().BeTrue();
        await _friendRequestRepository.Received(1).DeleteAsync(accepted.Id, Arg.Any<CancellationToken>());
    }
}
