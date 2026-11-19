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

    public static Guid CounterpartIdFor(this FriendRequest request, Guid viewerUserId) =>
        request.SenderUserId == viewerUserId ? request.TargetUserId : request.SenderUserId;

    public static FriendItemDto ToFriendDtoFor(
        this FriendRequest request,
        Guid viewerUserId,
        string? photoUrl = null)
    {
        bool isSender = request.SenderUserId == viewerUserId;
        return new FriendItemDto(
            UserId: isSender ? request.TargetUserId : request.SenderUserId,
            InviteCode: isSender ? request.TargetInviteCode : request.SenderInviteCode,
            DisplayName: isSender ? request.TargetName : request.SenderName,
            AddedVia: request.AddedVia,
            ConnectedAtUtc: request.RespondedAtUtc ?? request.CreatedAtUtc,
            PhotoUrl: photoUrl);
    }

    public static SentFriendRequestDto ToSentDto(this FriendRequest request, string? targetPhotoUrl = null) =>
        new(
            RequestId: request.Id,
            TargetInviteCode: request.TargetInviteCode,
            TargetName: request.TargetName,
            AddedVia: request.AddedVia,
            CreatedAtUtc: request.CreatedAtUtc,
            TargetPhotoUrl: targetPhotoUrl);

    public static SharedOfferItemDto ToDto(this SharedOffer item) =>
        new(
            Id: item.Id,
            OfferId: item.OfferId,
            SenderUserId: item.SenderUserId,
            RecipientUserId: item.RecipientUserId,
            OfferTitle: item.OfferTitle,
            StoreName: item.StoreName,
            MerchantId: item.MerchantId,
            Category: item.Category,
            OriginalPrice: item.OriginalPrice,
            DiscountedPrice: item.DiscountedPrice,
            DiscountPercentage: item.DiscountPercentage,
            ImageUrl: item.ImageUrl,
            SharedAtUtc: item.SharedAtUtc);

    public static async Task NotifyUserDevicesAsync(
        IDeviceRepository deviceRepository,
        IPushDispatcher pushDispatcher,
        INotificationSettingsRepository notificationSettings,
        Guid targetUserId,
        string title,
        string body,
        IReadOnlyDictionary<string, string> data,
        CancellationToken cancellationToken)
    {
        NotificationSettings settings = await notificationSettings.GetByUserIdAsync(targetUserId, cancellationToken)
            ?? NotificationSettings.CreateDefaults(targetUserId);
        if (!settings.PushEnabled || !settings.SocialAlerts)
        {
            return;
        }

        List<Device> devices = await deviceRepository.GetActiveByUserAsync(targetUserId, cancellationToken);
        foreach (Device device in devices)
        {
            PushResult result = await pushDispatcher.SendAsync(device.Token, title, body, data, cancellationToken);
            if (result == PushResult.InvalidToken)
            {
                await deviceRepository.DeactivateByTokenAsync(device.Id, device.Token, cancellationToken);
            }
        }
    }
}
