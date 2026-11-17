using MediatR;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Friends.Common;
using Sensus.Application.Friends.DTOs;
using Sensus.Domain.Common;
using Sensus.Domain.Entities;
using Sensus.Domain.Repositories;

namespace Sensus.Application.Friends.Commands.AcceptFriendRequest;

public sealed class AcceptFriendRequestCommandHandler
    : IRequestHandler<AcceptFriendRequestCommand, Result<FriendItemDto>>
{
    private readonly ICurrentUserService _currentUser;
    private readonly IFriendRequestRepository _friendRequestRepository;
    private readonly IUserRepository _userRepository;
    private readonly IDeviceRepository _deviceRepository;
    private readonly IPushDispatcher _pushDispatcher;
    private readonly IDateTimeProvider _dateTimeProvider;

    public AcceptFriendRequestCommandHandler(
        ICurrentUserService currentUser,
        IFriendRequestRepository friendRequestRepository,
        IUserRepository userRepository,
        IDeviceRepository deviceRepository,
        IPushDispatcher pushDispatcher,
        IDateTimeProvider dateTimeProvider)
    {
        _currentUser = currentUser;
        _friendRequestRepository = friendRequestRepository;
        _userRepository = userRepository;
        _deviceRepository = deviceRepository;
        _pushDispatcher = pushDispatcher;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<Result<FriendItemDto>> Handle(
        AcceptFriendRequestCommand request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.TryGetUserId(out Guid userId))
        {
            return Result.Failure<FriendItemDto>(
                Error.Unauthorized("Friends.Unauthorized", "Debes iniciar sesión para responder solicitudes."));
        }

        FriendRequest? friendRequest = await _friendRequestRepository.GetByIdAsync(request.RequestId, cancellationToken);
        if (friendRequest is null)
        {
            return Result.Failure<FriendItemDto>(
                Error.NotFound("Friends.RequestNotFound", "Solicitud no encontrada."));
        }

        if (friendRequest.TargetUserId != userId)
        {
            return Result.Failure<FriendItemDto>(
                Error.Forbidden("Friends.NotRecipient", "Solo quien recibió la solicitud puede aceptarla."));
        }

        Result acceptResult = friendRequest.Accept(_dateTimeProvider.UtcNow);
        if (acceptResult.IsFailure)
        {
            return Result.Failure<FriendItemDto>(acceptResult.Error);
        }

        await _friendRequestRepository.UpdateAsync(friendRequest, cancellationToken);

        User? senderUser = await _userRepository.GetByIdAsync(friendRequest.SenderUserId, cancellationToken);
        FriendItemDto dto = friendRequest.ToFriendDtoFor(userId, senderUser?.FotoPerfilUrl);
        if (senderUser is not null)
        {
            dto = dto with { DisplayName = senderUser.NombreCompleto, PhotoUrl = senderUser.FotoPerfilUrl };
        }

        await FriendRequestMappingExtensions.NotifyUserDevicesAsync(
            _deviceRepository,
            _pushDispatcher,
            friendRequest.SenderUserId,
            title: "Solicitud aceptada",
            body: $"{friendRequest.TargetName} aceptó tu solicitud de amistad.",
            data: new Dictionary<string, string>
            {
                ["type"] = "friend_accepted",
                ["requestId"] = friendRequest.Id.ToString()
            },
            cancellationToken);

        return Result.Success(dto);
    }
}
