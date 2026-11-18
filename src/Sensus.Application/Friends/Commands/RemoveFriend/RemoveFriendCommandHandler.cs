using MediatR;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Friends.Common;
using Sensus.Domain.Common;
using Sensus.Domain.Repositories;

namespace Sensus.Application.Friends.Commands.RemoveFriend;

public sealed class RemoveFriendCommandHandler : IRequestHandler<RemoveFriendCommand, Result>
{
    private readonly ICurrentUserService _currentUser;
    private readonly IFriendRequestRepository _friendRequestRepository;

    public RemoveFriendCommandHandler(
        ICurrentUserService currentUser,
        IFriendRequestRepository friendRequestRepository)
    {
        _currentUser = currentUser;
        _friendRequestRepository = friendRequestRepository;
    }

    public async Task<Result> Handle(
        RemoveFriendCommand request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.TryGetUserId(out var userId))
        {
            return Result.Failure(
                Error.Unauthorized("Friends.Unauthorized", "Debes iniciar sesión para administrar amigos."));
        }

        if (request.FriendUserId == Guid.Empty)
        {
            return Result.Failure(
                Error.Validation("Friends.InvalidFriendId", "El identificador del amigo no es válido."));
        }

        var existing = await _friendRequestRepository.GetBetweenUsersAsync(userId, request.FriendUserId, cancellationToken);
        if (existing is null)
        {
            return Result.Success();
        }

        await _friendRequestRepository.DeleteAsync(existing.Id, cancellationToken);
        return Result.Success();
    }
}
