using MediatR;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Friends.Common;
using Sensus.Application.Friends.DTOs;
using Sensus.Domain.Common;
using Sensus.Domain.Entities;
using Sensus.Domain.Enums;
using Sensus.Domain.Repositories;

namespace Sensus.Application.Friends.Commands.SendFriendRequest;

public sealed class SendFriendRequestCommandHandler
    : IRequestHandler<SendFriendRequestCommand, Result<SendFriendRequestResponseDto>>
{
    private readonly ICurrentUserService _currentUser;
    private readonly IUserRepository _userRepository;
    private readonly IFriendRequestRepository _friendRequestRepository;
    private readonly IDeviceRepository _deviceRepository;
    private readonly IPushDispatcher _pushDispatcher;
    private readonly IDateTimeProvider _dateTimeProvider;

    public SendFriendRequestCommandHandler(
        ICurrentUserService currentUser,
        IUserRepository userRepository,
        IFriendRequestRepository friendRequestRepository,
        IDeviceRepository deviceRepository,
        IPushDispatcher pushDispatcher,
        IDateTimeProvider dateTimeProvider)
    {
        _currentUser = currentUser;
        _userRepository = userRepository;
        _friendRequestRepository = friendRequestRepository;
        _deviceRepository = deviceRepository;
        _pushDispatcher = pushDispatcher;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<Result<SendFriendRequestResponseDto>> Handle(
        SendFriendRequestCommand request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.TryGetUserId(out var senderId))
        {
            return Result.Failure<SendFriendRequestResponseDto>(
                Error.Unauthorized("Friends.Unauthorized", "Debes iniciar sesión para enviar solicitudes."));
        }

        var normalizedCode = request.InviteCode?.Trim().ToUpperInvariant() ?? string.Empty;
        if (normalizedCode.Length < 6)
        {
            return Result.Failure<SendFriendRequestResponseDto>(
                Error.Validation("Friends.InvalidCode", "Ingresa un código de invitación válido."));
        }

        var sender = await _userRepository.GetByIdAsync(senderId, cancellationToken);
        if (sender is null)
        {
            return Result.Failure<SendFriendRequestResponseDto>(
                Error.NotFound("User.NotFound", "Usuario no encontrado."));
        }

        var target = await _userRepository.GetByInviteCodeAsync(normalizedCode, cancellationToken);
        if (target is null || !target.Estatus)
        {
            return Result.Failure<SendFriendRequestResponseDto>(
                Error.NotFound("Friends.InviteCodeNotFound", "No encontramos un vecino activo con ese código."));
        }

        if (target.Id == sender.Id)
        {
            return Result.Failure<SendFriendRequestResponseDto>(
                Error.Validation("Friends.SelfInvite", "No puedes enviarte una solicitud a ti mismo."));
        }

        var existing = await _friendRequestRepository.GetBetweenUsersAsync(sender.Id, target.Id, cancellationToken);
        if (existing is not null)
        {
            if (existing.Status == FriendRequestStatus.Accepted)
            {
                return Result.Success(new SendFriendRequestResponseDto(
                    Outcome: "AlreadyConnected",
                    Friend: existing.ToFriendDtoFor(sender.Id),
                    PendingRequest: null));
            }

            if (existing.Status == FriendRequestStatus.Pending)
            {
                return await HandleExistingPendingAsync(existing, sender, target, cancellationToken);
            }

            await _friendRequestRepository.DeleteAsync(existing.Id, cancellationToken);
        }

        var senderCode = FriendInviteCodeGenerator.Generate(sender.Correo, sender.NombreCompleto);
        var createResult = FriendRequest.Create(
            sender.Id,
            sender.NombreCompleto,
            senderCode,
            target.Id,
            target.NombreCompleto,
            normalizedCode,
            request.AddedVia ?? "QR",
            _dateTimeProvider.UtcNow);

        if (createResult.IsFailure)
        {
            return Result.Failure<SendFriendRequestResponseDto>(createResult.Error);
        }

        var friendRequest = createResult.Value;
        await _friendRequestRepository.AddAsync(friendRequest, cancellationToken);

        await FriendRequestMappingExtensions.NotifyUserDevicesAsync(
            _deviceRepository,
            _pushDispatcher,
            target.Id,
            title: "Nueva solicitud de amistad",
            body: $"{sender.NombreCompleto} quiere agregarte como amigo en Sensus.",
            data: new Dictionary<string, string>
            {
                ["type"] = "friend_request",
                ["requestId"] = friendRequest.Id.ToString(),
                ["senderInviteCode"] = senderCode
            },
            cancellationToken);

        return Result.Success(new SendFriendRequestResponseDto(
            Outcome: "Sent",
            Friend: null,
            PendingRequest: friendRequest.ToSentDto()));
    }

    private async Task<Result<SendFriendRequestResponseDto>> HandleExistingPendingAsync(
        FriendRequest existing,
        User sender,
        User target,
        CancellationToken cancellationToken)
    {
        if (existing.SenderUserId == sender.Id)
        {
            return Result.Success(new SendFriendRequestResponseDto(
                Outcome: "AlreadyPending",
                Friend: null,
                PendingRequest: existing.ToSentDto()));
        }

        existing.Accept(_dateTimeProvider.UtcNow);
        await _friendRequestRepository.UpdateAsync(existing, cancellationToken);

        await FriendRequestMappingExtensions.NotifyUserDevicesAsync(
            _deviceRepository,
            _pushDispatcher,
            target.Id,
            title: "Solicitud aceptada",
            body: $"{sender.NombreCompleto} y tú ahora son amigos en Sensus.",
            data: new Dictionary<string, string>
            {
                ["type"] = "friend_accepted",
                ["requestId"] = existing.Id.ToString()
            },
            cancellationToken);

        return Result.Success(new SendFriendRequestResponseDto(
            Outcome: "AcceptedMutual",
            Friend: existing.ToFriendDtoFor(sender.Id),
            PendingRequest: null));
    }
}
