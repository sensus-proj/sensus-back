namespace Sensus.Application.Friends.DTOs;

public sealed record FriendItemDto(
    Guid UserId,
    string InviteCode,
    string DisplayName,
    string AddedVia,
    DateTime ConnectedAtUtc);

public sealed record PendingFriendRequestDto(
    Guid RequestId,
    string SenderInviteCode,
    string SenderName,
    string AddedVia,
    DateTime CreatedAtUtc);

public sealed record SentFriendRequestDto(
    Guid RequestId,
    string TargetInviteCode,
    string TargetName,
    string AddedVia,
    DateTime CreatedAtUtc);

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
