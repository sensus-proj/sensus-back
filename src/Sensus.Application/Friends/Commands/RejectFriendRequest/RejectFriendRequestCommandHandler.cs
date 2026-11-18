using MediatR;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Friends.Common;
using Sensus.Domain.Common;
using Sensus.Domain.Repositories;

namespace Sensus.Application.Friends.Commands.RejectFriendRequest;

public sealed class RejectFriendRequestCommandHandler : IRequestHandler<RejectFriendRequestCommand, Result>
{
    private readonly ICurrentUserService _currentUser;
    private readonly IFriendRequestRepository _friendRequestRepository;
    private readonly IDateTimeProvider _dateTimeProvider;

    public RejectFriendRequestCommandHandler(
        ICurrentUserService currentUser,
        IFriendRequestRepository friendRequestRepository,
        IDateTimeProvider dateTimeProvider)
    {
        _currentUser = currentUser;
        _friendRequestRepository = friendRequestRepository;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<Result> Handle(
        RejectFriendRequestCommand request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.TryGetUserId(out var userId))
        {
            return Result.Failure(
                Error.Unauthorized("Friends.Unauthorized", "Debes iniciar sesión para cancelar solicitudes."));
        }

        var friendRequest = await _friendRequestRepository.GetByIdAsync(request.RequestId, cancellationToken);
        if (friendRequest is null)
        {
            return Result.Failure(
                Error.NotFound("Friends.RequestNotFound", "Solicitud no encontrada."));
        }

        if (friendRequest.TargetUserId != userId && friendRequest.SenderUserId != userId)
        {
            return Result.Failure(
                Error.Forbidden("Friends.NotParticipant", "No tienes permiso para modificar esta solicitud."));
        }

        friendRequest.Reject(_dateTimeProvider.UtcNow);
        await _friendRequestRepository.UpdateAsync(friendRequest, cancellationToken);
        return Result.Success();
    }
}
