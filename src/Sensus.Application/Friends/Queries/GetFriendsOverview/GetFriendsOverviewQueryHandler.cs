using MediatR;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Friends.Common;
using Sensus.Application.Friends.DTOs;
using Sensus.Domain.Common;
using Sensus.Domain.Enums;
using Sensus.Domain.Repositories;

namespace Sensus.Application.Friends.Queries.GetFriendsOverview;

public sealed class GetFriendsOverviewQueryHandler : IRequestHandler<GetFriendsOverviewQuery, Result<FriendsOverviewDto>>
{
    private readonly ICurrentUserService _currentUser;
    private readonly IUserRepository _userRepository;
    private readonly IFriendRequestRepository _friendRequestRepository;

    public GetFriendsOverviewQueryHandler(
        ICurrentUserService currentUser,
        IUserRepository userRepository,
        IFriendRequestRepository friendRequestRepository)
    {
        _currentUser = currentUser;
        _userRepository = userRepository;
        _friendRequestRepository = friendRequestRepository;
    }

    public async Task<Result<FriendsOverviewDto>> Handle(
        GetFriendsOverviewQuery request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.TryGetUserId(out var userId))
        {
            return Result.Failure<FriendsOverviewDto>(
                Error.Unauthorized("Friends.Unauthorized", "Debes iniciar sesión para ver tus amigos."));
        }

        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        if (user is null)
        {
            return Result.Failure<FriendsOverviewDto>(
                Error.NotFound("User.NotFound", "Usuario no encontrado."));
        }

        var myInviteCode = FriendInviteCodeGenerator.Generate(user.Correo, user.NombreCompleto);
        var records = await _friendRequestRepository.GetForUserAsync(userId, cancellationToken);

        var friends = records
            .Where(r => r.Status == FriendRequestStatus.Accepted)
            .OrderByDescending(r => r.RespondedAtUtc ?? r.CreatedAtUtc)
            .Select(r => r.ToFriendDtoFor(userId))
            .ToList();

        var pendingReceived = records
            .Where(r => r.Status == FriendRequestStatus.Pending && r.TargetUserId == userId)
            .OrderByDescending(r => r.CreatedAtUtc)
            .Select(r => new PendingFriendRequestDto(
                r.Id,
                r.SenderInviteCode,
                r.SenderName,
                r.AddedVia,
                r.CreatedAtUtc))
            .ToList();

        var pendingSent = records
            .Where(r => r.Status == FriendRequestStatus.Pending && r.SenderUserId == userId)
            .OrderByDescending(r => r.CreatedAtUtc)
            .Select(r => r.ToSentDto())
            .ToList();

        return Result.Success(new FriendsOverviewDto(
            MyInviteCode: myInviteCode,
            Friends: friends,
            PendingReceived: pendingReceived,
            PendingSent: pendingSent));
    }
}
