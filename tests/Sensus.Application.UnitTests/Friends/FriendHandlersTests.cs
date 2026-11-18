using FluentAssertions;
using NSubstitute;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Friends.Commands.AcceptFriendRequest;
using Sensus.Application.Friends.Commands.RejectFriendRequest;
using Sensus.Application.Friends.Commands.RemoveFriend;
using Sensus.Application.Friends.Commands.SendFriendRequest;
using Sensus.Application.Friends.Commands.ShareOfferWithFriends;
using Sensus.Application.Friends.DTOs;
using Sensus.Application.Friends.Queries.GetFriendSharedOffers;
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
    private readonly ISharedOfferRepository _sharedOfferRepository = Substitute.For<ISharedOfferRepository>();
    private readonly IOfferRepository _offerRepository = Substitute.For<IOfferRepository>();
    private readonly IComercioRepository _comercioRepository = Substitute.For<IComercioRepository>();
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

        SharedOffer.Create(Guid.Empty, targetId, Guid.NewGuid(), "Pan", "Tienda", null, null, 20m, 10m, 50, null, FixedNow).IsFailure.Should().BeTrue();
        SharedOffer.Create(senderId, senderId, Guid.NewGuid(), "Pan", "Tienda", null, null, 20m, 10m, 50, null, FixedNow).IsFailure.Should().BeTrue();
        SharedOffer.Create(senderId, targetId, Guid.NewGuid(), " ", "Tienda", null, null, 20m, 10m, 50, null, FixedNow).IsFailure.Should().BeTrue();

        SharedOffer shared = SharedOffer.Create(senderId, targetId, Guid.NewGuid(), "Pan dulce", " ", null, "Panadería", 40m, 20m, 50, "https://img", FixedNow).Value;
        shared.StoreName.Should().Be("Comercio local");
        SharedOffer rehydratedShare = SharedOffer.Rehydrate(
            shared.Id, shared.SenderUserId, shared.RecipientUserId, shared.OfferId, shared.OfferTitle,
            shared.StoreName, shared.MerchantId, shared.Category, shared.OriginalPrice,
            shared.DiscountedPrice, shared.DiscountPercentage, shared.ImageUrl, shared.SharedAtUtc);
        rehydratedShare.OfferTitle.Should().Be("Pan dulce");
    }

    [Fact]
    public async Task SendFriendRequest_CreatesPendingRequestAndNotifiesTargetDevices()
    {
        User sender = User.Create("Alberto", "alberto@sensus.com", "hash", true, FixedNow).Value;
        User target = User.Create("Maria", "maria@sensus.com", "hash", true, FixedNow).Value;
        target.UpdateProfile("Maria", "https://cdn/maria.jpg");
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
        result.Value.PendingRequest.TargetPhotoUrl.Should().Be("https://cdn/maria.jpg");
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
        User senderUser = User.Create("Carlos", "carlos@sensus.com", "hash", true, FixedNow).Value;
        senderUser.UpdateProfile("Carlos", "https://cdn/carlos.jpg");
        Guid senderId = senderUser.Id;
        Guid targetId = Guid.NewGuid();
        Guid strangerId = Guid.NewGuid();
        FriendRequest request = FriendRequest.Create(senderId, "Carlos", "SNS-111111", targetId, "Lucia", "SNS-222222", "QR", FixedNow).Value;
        Device device = Device.Create(senderId, "dev-s", "Android", "token-12345678901234567890", FixedNow);

        AcceptFriendRequestCommandHandler acceptHandler = new(
            _currentUser, _friendRequestRepository, _userRepository, _deviceRepository, _pushDispatcher, _dateTimeProvider);
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
        _userRepository.GetByIdAsync(senderId, Arg.Any<CancellationToken>()).Returns(senderUser);
        _deviceRepository.GetActiveByUserAsync(senderId, Arg.Any<CancellationToken>()).Returns([device]);

        Result<FriendItemDto> accepted = await acceptHandler.Handle(new AcceptFriendRequestCommand(request.Id), CancellationToken.None);
        accepted.IsSuccess.Should().BeTrue();
        accepted.Value.DisplayName.Should().Be("Carlos");
        accepted.Value.PhotoUrl.Should().Be("https://cdn/carlos.jpg");

        Result rejected = await rejectHandler.Handle(new RejectFriendRequestCommand(request.Id), CancellationToken.None);
        rejected.IsSuccess.Should().BeTrue();
        request.Status.Should().Be(FriendRequestStatus.Rejected);

        (await acceptHandler.Handle(new AcceptFriendRequestCommand(request.Id), CancellationToken.None)).Error.Type.Should().Be(ErrorType.Conflict);
    }

    [Fact]
    public async Task GetFriendsOverviewAndRemoveFriend_CoverPhotoEnrichmentAndAllBranches()
    {
        User user = User.Create("Lucia", "lucia@sensus.com", "hash", true, FixedNow).Value;
        User friendUser = User.Create("Ana", "ana@sensus.com", "hash", true, FixedNow).Value;
        friendUser.UpdateProfile("Ana Actualizada", "https://cdn/ana.jpg");
        Guid friendId = friendUser.Id;
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
        _userRepository.GetByIdAsync(friendId, Arg.Any<CancellationToken>()).Returns(friendUser);
        _userRepository.GetByIdAsync(incomingId, Arg.Any<CancellationToken>()).Returns((User?)null);
        _userRepository.GetByIdAsync(outgoingId, Arg.Any<CancellationToken>()).Returns((User?)null);
        _friendRequestRepository.GetForUserAsync(user.Id, Arg.Any<CancellationToken>()).Returns([accepted, pendingIn, pendingOut]);

        Result<FriendsOverviewDto> overview = await overviewHandler.Handle(new GetFriendsOverviewQuery(), CancellationToken.None);
        overview.IsSuccess.Should().BeTrue();
        overview.Value.MyInviteCode.Should().StartWith("SNS-");
        overview.Value.Friends.Should().ContainSingle(f => f.DisplayName == "Ana Actualizada" && f.PhotoUrl == "https://cdn/ana.jpg");
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

    [Fact]
    public async Task ShareOfferAndGetThread_CoverValidationMaxTenFriendsAndThreadRetrieval()
    {
        User sender = User.Create("Alberto", "alberto@sensus.com", "hash", true, FixedNow).Value;
        User friend1 = User.Create("Ana", "ana@sensus.com", "hash", true, FixedNow).Value;
        friend1.UpdateProfile("Ana", "https://cdn/ana.jpg");
        User friend2 = User.Create("Luis", "luis@sensus.com", "hash", true, FixedNow).Value;

        Comercio comercio = Comercio.Reconstitute(
            Guid.NewGuid(), Guid.NewGuid(), "Panadería Central", "Av Hidalgo 10", "42850", 20.0, -99.3, true, FixedNow, MerchantCategory.Restaurantes);
        Offer offer = Offer.Create(
            comercio.Id, "Bolillos artesanales", "Recién horneados del día", 50m, 40, 5, FixedNow.AddHours(-1), FixedNow.AddHours(4), "https://cdn/offer.jpg", 20.0, -99.3, FixedNow, "Panadería").Value;

        FriendRequest rel1 = FriendRequest.Create(sender.Id, "Alberto", "SNS-1", friend1.Id, "Ana", "SNS-2", "QR", FixedNow).Value;
        rel1.Accept(FixedNow);
        FriendRequest rel2 = FriendRequest.Create(friend2.Id, "Luis", "SNS-3", sender.Id, "Alberto", "SNS-1", "QR", FixedNow).Value;
        rel2.Accept(FixedNow);

        ShareOfferWithFriendsCommandHandler shareHandler = new(
            _currentUser, _userRepository, _offerRepository, _comercioRepository, _friendRequestRepository, _sharedOfferRepository, _deviceRepository, _pushDispatcher, _dateTimeProvider);
        GetFriendSharedOffersQueryHandler threadHandler = new(
            _currentUser, _userRepository, _friendRequestRepository, _sharedOfferRepository);

        _currentUser.UserId.Returns((string?)null);
        (await shareHandler.Handle(new ShareOfferWithFriendsCommand(offer.Id, [friend1.Id]), CancellationToken.None)).Error.Type.Should().Be(ErrorType.Unauthorized);
        (await threadHandler.Handle(new GetFriendSharedOffersQuery(friend1.Id), CancellationToken.None)).Error.Type.Should().Be(ErrorType.Unauthorized);

        _currentUser.UserId.Returns(sender.Id.ToString());
        (await shareHandler.Handle(new ShareOfferWithFriendsCommand(offer.Id, []), CancellationToken.None)).Error.Type.Should().Be(ErrorType.Validation);

        List<Guid> elevenIds = Enumerable.Range(0, 11).Select(_ => Guid.NewGuid()).ToList();
        (await shareHandler.Handle(new ShareOfferWithFriendsCommand(offer.Id, elevenIds), CancellationToken.None)).Error.Type.Should().Be(ErrorType.Validation);

        _offerRepository.GetByIdAsync(offer.Id, Arg.Any<CancellationToken>()).Returns((Offer?)null);
        (await shareHandler.Handle(new ShareOfferWithFriendsCommand(offer.Id, [friend1.Id]), CancellationToken.None)).Error.Type.Should().Be(ErrorType.NotFound);

        _offerRepository.GetByIdAsync(offer.Id, Arg.Any<CancellationToken>()).Returns(offer);
        _userRepository.GetByIdAsync(sender.Id, Arg.Any<CancellationToken>()).Returns(sender);
        _friendRequestRepository.GetForUserAsync(sender.Id, Arg.Any<CancellationToken>()).Returns([rel1, rel2]);
        _comercioRepository.GetByIdAsync(offer.MerchantId, Arg.Any<CancellationToken>()).Returns(comercio);
        _deviceRepository.GetActiveByUserAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns([]);

        (await shareHandler.Handle(new ShareOfferWithFriendsCommand(offer.Id, [Guid.NewGuid()]), CancellationToken.None)).Error.Type.Should().Be(ErrorType.Forbidden);

        Result<ShareOfferResultDto> sharedResult = await shareHandler.Handle(
            new ShareOfferWithFriendsCommand(offer.Id, [friend1.Id, friend2.Id]), CancellationToken.None);
        sharedResult.IsSuccess.Should().BeTrue();
        sharedResult.Value.SharedCount.Should().Be(2);
        sharedResult.Value.Items.Should().OnlyContain(i => i.OfferTitle == "Bolillos artesanales" && i.StoreName == "Panadería Central");
        await _sharedOfferRepository.Received(1).AddRangeAsync(
            Arg.Is<IReadOnlyCollection<SharedOffer>>(c => c.Count == 2), Arg.Any<CancellationToken>());

        (await threadHandler.Handle(new GetFriendSharedOffersQuery(Guid.Empty), CancellationToken.None)).Error.Type.Should().Be(ErrorType.Validation);
        _friendRequestRepository.GetBetweenUsersAsync(sender.Id, friend1.Id, Arg.Any<CancellationToken>()).Returns((FriendRequest?)null);
        (await threadHandler.Handle(new GetFriendSharedOffersQuery(friend1.Id), CancellationToken.None)).Error.Type.Should().Be(ErrorType.Forbidden);

        SharedOffer threadMsg = SharedOffer.Create(sender.Id, friend1.Id, offer.Id, offer.Title, comercio.NombreComercial, comercio.Id, "Panadería", 50m, 30m, 40, offer.ImageUrl, FixedNow).Value;
        _friendRequestRepository.GetBetweenUsersAsync(sender.Id, friend1.Id, Arg.Any<CancellationToken>()).Returns(rel1);
        _userRepository.GetByIdAsync(friend1.Id, Arg.Any<CancellationToken>()).Returns(friend1);
        _sharedOfferRepository.GetConversationAsync(sender.Id, friend1.Id, Arg.Any<CancellationToken>()).Returns([threadMsg]);

        Result<FriendSharedOffersThreadDto> thread = await threadHandler.Handle(new GetFriendSharedOffersQuery(friend1.Id), CancellationToken.None);
        thread.IsSuccess.Should().BeTrue();
        thread.Value.Friend.PhotoUrl.Should().Be("https://cdn/ana.jpg");
        thread.Value.Offers.Should().ContainSingle(o => o.OfferId == offer.Id);
    }
}
