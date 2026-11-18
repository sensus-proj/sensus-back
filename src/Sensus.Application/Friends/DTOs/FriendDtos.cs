namespace Sensus.Application.Friends.DTOs;

public sealed record FriendItemDto(
    Guid UserId,
    string InviteCode,
    string DisplayName,
    string AddedVia,
    DateTime ConnectedAtUtc,
    string? PhotoUrl = null);

public sealed record PendingFriendRequestDto(
    Guid RequestId,
    string SenderInviteCode,
    string SenderName,
    string AddedVia,
    DateTime CreatedAtUtc,
    string? SenderPhotoUrl = null);

public sealed record SentFriendRequestDto(
    Guid RequestId,
    string TargetInviteCode,
    string TargetName,
    string AddedVia,
    DateTime CreatedAtUtc,
    string? TargetPhotoUrl = null);

public sealed record FriendsOverviewDto(
    string MyInviteCode,
    IReadOnlyList<FriendItemDto> Friends,
    IReadOnlyList<PendingFriendRequestDto> PendingReceived,
    IReadOnlyList<SentFriendRequestDto> PendingSent);

public sealed record SendFriendRequestInput(
    string InviteCode,
    string? AddedVia = "QR");

public sealed record SendFriendRequestResponseDto(
    string Outcome,
    FriendItemDto? Friend,
    SentFriendRequestDto? PendingRequest);

public sealed record ShareOfferRequestDto(
    IReadOnlyList<Guid> RecipientUserIds);

public sealed record SharedOfferItemDto(
    Guid Id,
    Guid OfferId,
    Guid SenderUserId,
    Guid RecipientUserId,
    string OfferTitle,
    string StoreName,
    Guid? MerchantId,
    string? Category,
    decimal OriginalPrice,
    decimal DiscountedPrice,
    int DiscountPercentage,
    string? ImageUrl,
    DateTime SharedAtUtc);

public sealed record ShareOfferResultDto(
    int SharedCount,
    IReadOnlyList<SharedOfferItemDto> Items);

public sealed record FriendSharedOffersThreadDto(
    FriendItemDto Friend,
    IReadOnlyList<SharedOfferItemDto> Offers);
