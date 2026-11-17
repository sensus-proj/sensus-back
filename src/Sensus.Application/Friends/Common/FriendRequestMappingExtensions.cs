using Sensus.Application.Common.Interfaces;
using Sensus.Application.Friends.DTOs;
using Sensus.Domain.Entities;
using Sensus.Domain.Repositories;

namespace Sensus.Application.Friends.Common;

internal static class FriendRequestMappingExtensions
{
    public static bool TryGetUserId(this ICurrentUserService currentUser, out Guid userId)
    {
        if (currentUser.IsGuest || string.IsNullOrWhiteSpace(currentUser.UserId))
        {
            userId = Guid.Empty;
            return false;
        }

        return Guid.TryParse(currentUser.UserId, out userId);
    }

    public static FriendItemDto ToFriendDtoFor(this FriendRequest request, Guid viewerUserId)
    {
        var isSender = request.SenderUserId == viewerUserId;
        return new FriendItemDto(
            UserId: isSender ? request.TargetUserId : request.SenderUserId,
            InviteCode: isSender ? request.TargetInviteCode : request.SenderInviteCode,
            DisplayName: isSender ? request.TargetName : request.SenderName,
            AddedVia: request.AddedVia,
            ConnectedAtUtc: request.RespondedAtUtc ?? request.CreatedAtUtc);
    }

    public static SentFriendRequestDto ToSentDto(this FriendRequest request) =>
        new(
            RequestId: request.Id,
            TargetInviteCode: request.TargetInviteCode,
            TargetName: request.TargetName,
            AddedVia: request.AddedVia,
            CreatedAtUtc: request.CreatedAtUtc);

    public static async Task NotifyUserDevicesAsync(
        IDeviceRepository deviceRepository,
        IPushDispatcher pushDispatcher,
        Guid targetUserId,
        string title,
        string body,
        IReadOnlyDictionary<string, string> data,
        CancellationToken cancellationToken)
    {
        var devices = await deviceRepository.GetActiveByUserAsync(targetUserId, cancellationToken);
        foreach (var device in devices)
        {
            await pushDispatcher.SendAsync(device.Token, title, body, data, cancellationToken);
        }
    }
}
