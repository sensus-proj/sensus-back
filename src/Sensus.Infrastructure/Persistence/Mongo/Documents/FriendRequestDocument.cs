using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using Sensus.Domain.Entities;
using Sensus.Domain.Enums;

namespace Sensus.Infrastructure.Persistence.Mongo.Documents;

/// <summary>
/// Documento de solicitud o vínculo de amistad en MongoDB Atlas.
/// </summary>
public sealed class FriendRequestDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public Guid Id { get; set; }

    [BsonElement("senderUserId")]
    [BsonRepresentation(BsonType.String)]
    public Guid SenderUserId { get; set; }

    [BsonElement("senderName")]
    public string SenderName { get; set; } = string.Empty;

    [BsonElement("senderInviteCode")]
    public string SenderInviteCode { get; set; } = string.Empty;

    [BsonElement("targetUserId")]
    [BsonRepresentation(BsonType.String)]
    public Guid TargetUserId { get; set; }

    [BsonElement("targetName")]
    public string TargetName { get; set; } = string.Empty;

    [BsonElement("targetInviteCode")]
    public string TargetInviteCode { get; set; } = string.Empty;

    [BsonElement("addedVia")]
    public string AddedVia { get; set; } = "QR";

    [BsonElement("status")]
    public FriendRequestStatus Status { get; set; }

    [BsonElement("createdAtUtc")]
    public DateTime CreatedAtUtc { get; set; }

    [BsonElement("respondedAtUtc")]
    public DateTime? RespondedAtUtc { get; set; }

    public static FriendRequestDocument FromDomain(FriendRequest entity) =>
        new()
        {
            Id = entity.Id,
            SenderUserId = entity.SenderUserId,
            SenderName = entity.SenderName,
            SenderInviteCode = entity.SenderInviteCode,
            TargetUserId = entity.TargetUserId,
            TargetName = entity.TargetName,
            TargetInviteCode = entity.TargetInviteCode,
            AddedVia = entity.AddedVia,
            Status = entity.Status,
            CreatedAtUtc = entity.CreatedAtUtc,
            RespondedAtUtc = entity.RespondedAtUtc
        };

    public FriendRequest ToDomain() =>
        FriendRequest.Rehydrate(
            Id,
            SenderUserId,
            SenderName,
            SenderInviteCode,
            TargetUserId,
            TargetName,
            TargetInviteCode,
            AddedVia,
            Status,
            CreatedAtUtc,
            RespondedAtUtc);
}
