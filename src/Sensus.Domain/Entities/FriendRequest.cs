using Sensus.Domain.Common;
using Sensus.Domain.Enums;

namespace Sensus.Domain.Entities;

/// <summary>
/// Solicitud o vínculo de amistad entre dos usuarios de Sensus.
/// </summary>
public sealed class FriendRequest : BaseEntity, IAggregateRoot
{
    public Guid SenderUserId { get; private set; }
    public string SenderName { get; private set; } = string.Empty;
    public string SenderInviteCode { get; private set; } = string.Empty;
    public Guid TargetUserId { get; private set; }
    public string TargetName { get; private set; } = string.Empty;
    public string TargetInviteCode { get; private set; } = string.Empty;
    public string AddedVia { get; private set; } = "QR";
    public FriendRequestStatus Status { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? RespondedAtUtc { get; private set; }

    private FriendRequest() : base()
    {
    }

    private FriendRequest(
        Guid id,
        Guid senderUserId,
        string senderName,
        string senderInviteCode,
        Guid targetUserId,
        string targetName,
        string targetInviteCode,
        string addedVia,
        FriendRequestStatus status,
        DateTime createdAtUtc,
        DateTime? respondedAtUtc) : base(id)
    {
        SenderUserId = senderUserId;
        SenderName = senderName;
        SenderInviteCode = senderInviteCode;
        TargetUserId = targetUserId;
        TargetName = targetName;
        TargetInviteCode = targetInviteCode;
        AddedVia = addedVia;
        Status = status;
        CreatedAtUtc = createdAtUtc;
        RespondedAtUtc = respondedAtUtc;
    }

    public static Result<FriendRequest> Create(
        Guid senderUserId,
        string senderName,
        string senderInviteCode,
        Guid targetUserId,
        string targetName,
        string targetInviteCode,
        string addedVia,
        DateTime createdAtUtc)
    {
        if (senderUserId == Guid.Empty || targetUserId == Guid.Empty)
        {
            return Result.Failure<FriendRequest>(
                Error.Validation("Friend.InvalidUsers", "Los identificadores de usuario no pueden estar vacíos."));
        }

        if (senderUserId == targetUserId)
        {
            return Result.Failure<FriendRequest>(
                Error.Validation("Friend.SelfInvite", "No puedes enviarte una solicitud de amistad a ti mismo."));
        }

        if (string.IsNullOrWhiteSpace(senderInviteCode) || string.IsNullOrWhiteSpace(targetInviteCode))
        {
            return Result.Failure<FriendRequest>(
                Error.Validation("Friend.InvalidCode", "El código de invitación es obligatorio."));
        }

        var cleanSenderName = string.IsNullOrWhiteSpace(senderName) ? senderInviteCode.Trim() : senderName.Trim();
        var cleanTargetName = string.IsNullOrWhiteSpace(targetName) ? targetInviteCode.Trim() : targetName.Trim();
        var cleanVia = string.IsNullOrWhiteSpace(addedVia) ? "QR" : addedVia.Trim();

        return Result.Success(new FriendRequest(
            Guid.NewGuid(),
            senderUserId,
            cleanSenderName,
            senderInviteCode.Trim().ToUpperInvariant(),
            targetUserId,
            cleanTargetName,
            targetInviteCode.Trim().ToUpperInvariant(),
            cleanVia,
            FriendRequestStatus.Pending,
            createdAtUtc,
            null));
    }

    public static FriendRequest Rehydrate(
        Guid id,
        Guid senderUserId,
        string senderName,
        string senderInviteCode,
        Guid targetUserId,
        string targetName,
        string targetInviteCode,
        string addedVia,
        FriendRequestStatus status,
        DateTime createdAtUtc,
        DateTime? respondedAtUtc)
    {
        return new FriendRequest(
            id,
            senderUserId,
            senderName,
            senderInviteCode,
            targetUserId,
            targetName,
            targetInviteCode,
            addedVia,
            status,
            createdAtUtc,
            respondedAtUtc);
    }

    public Result Accept(DateTime respondedAtUtc)
    {
        if (Status == FriendRequestStatus.Accepted)
        {
            return Result.Success();
        }

        if (Status != FriendRequestStatus.Pending)
        {
            return Result.Failure(
                Error.Conflict("Friend.AlreadyProcessed", "La solicitud ya fue procesada anteriormente."));
        }

        Status = FriendRequestStatus.Accepted;
        RespondedAtUtc = respondedAtUtc;
        return Result.Success();
    }

    public Result Reject(DateTime respondedAtUtc)
    {
        if (Status == FriendRequestStatus.Rejected)
        {
            return Result.Success();
        }

        Status = FriendRequestStatus.Rejected;
        RespondedAtUtc = respondedAtUtc;
        return Result.Success();
    }
}
