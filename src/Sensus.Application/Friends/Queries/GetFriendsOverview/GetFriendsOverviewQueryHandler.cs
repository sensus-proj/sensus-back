using MediatR;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Friends.Common;
using Sensus.Application.Friends.DTOs;
using Sensus.Domain.Common;
using Sensus.Domain.Entities;
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
        if (!_currentUser.TryGetUserId(out Guid userId))
        {
            return Result.Failure<FriendsOverviewDto>(
                Error.Unauthorized("Friends.Unauthorized", "Debes iniciar sesión para ver tus amigos."));
        }

        User? user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        if (user is null)
        {
            return Result.Failure<FriendsOverviewDto>(
                Error.NotFound("User.NotFound", "Usuario no encontrado."));
        }

        string myInviteCode = FriendInviteCodeGenerator.Generate(user.Correo, user.NombreCompleto);
        IReadOnlyList<FriendRequest> records = await _friendRequestRepository.GetForUserAsync(userId, cancellationToken);

        Dictionary<Guid, User?> counterpartUsers = await LoadCounterpartUsersAsync(records, userId, cancellationToken);

        List<FriendItemDto> friends = records
            .Where(r => r.Status == FriendRequestStatus.Accepted)
            .OrderByDescending(r => r.RespondedAtUtc ?? r.CreatedAtUtc)
            .Select(r =>
            {
                Guid counterpartId = r.CounterpartIdFor(userId);
                User? counterpart = counterpartUsers.GetValueOrDefault(counterpartId);
                FriendItemDto baseDto = r.ToFriendDtoFor(userId, counterpart?.FotoPerfilUrl);
                return counterpart is null
                    ? baseDto
                    : baseDto with { DisplayName = counterpart.NombreCompleto, PhotoUrl = counterpart.FotoPerfilUrl };
            })
            .ToList();

        List<PendingFriendRequestDto> pendingReceived = records
            .Where(r => r.Status == FriendRequestStatus.Pending && r.TargetUserId == userId)
            .OrderByDescending(r => r.CreatedAtUtc)
            .Select(r =>
            {
                User? senderUser = counterpartUsers.GetValueOrDefault(r.SenderUserId);
                return new PendingFriendRequestDto(
                    r.Id,
                    r.SenderInviteCode,
                    senderUser?.NombreCompleto ?? r.SenderName,
                    r.AddedVia,
                    r.CreatedAtUtc,
                    senderUser?.FotoPerfilUrl);
            })
            .ToList();

        List<SentFriendRequestDto> pendingSent = records
            .Where(r => r.Status == FriendRequestStatus.Pending && r.SenderUserId == userId)
            .OrderByDescending(r => r.CreatedAtUtc)
            .Select(r =>
            {
                User? targetUser = counterpartUsers.GetValueOrDefault(r.TargetUserId);
                SentFriendRequestDto sentDto = r.ToSentDto(targetUser?.FotoPerfilUrl);
                return targetUser is null
                    ? sentDto
                    : sentDto with { TargetName = targetUser.NombreCompleto, TargetPhotoUrl = targetUser.FotoPerfilUrl };
            })
            .ToList();

        return Result.Success(new FriendsOverviewDto(
            MyInviteCode: myInviteCode,
            Friends: friends,
            PendingReceived: pendingReceived,
            PendingSent: pendingSent));
    }

    private async Task<Dictionary<Guid, User?>> LoadCounterpartUsersAsync(
        IReadOnlyList<FriendRequest> records,
        Guid currentUserId,
        CancellationToken cancellationToken)
    {
        List<Guid> ids = records
            .Where(r => r.Status is FriendRequestStatus.Accepted or FriendRequestStatus.Pending)
            .Select(r => r.CounterpartIdFor(currentUserId))
            .Distinct()
            .ToList();

        Dictionary<Guid, User?> map = new(ids.Count);
        foreach (Guid id in ids)
        {
            map[id] = await _userRepository.GetByIdAsync(id, cancellationToken);
        }

        return map;
    }
}
