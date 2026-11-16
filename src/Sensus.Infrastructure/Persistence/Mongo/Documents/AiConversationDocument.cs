using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using Sensus.Domain.Entities;
using Sensus.Domain.Enums;

namespace Sensus.Infrastructure.Persistence.Mongo.Documents;

public sealed class AiConversationDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public Guid Id { get; set; }

    [BsonRepresentation(BsonType.String)]
    [BsonElement("ownerUserId")]
    public Guid OwnerUserId { get; set; }

    [BsonElement("ownerRole")]
    public string OwnerRole { get; set; } = RoleType.Consumidor;

    [BsonElement("status")]
    public string Status { get; set; } = AiConversationStatus.Active.ToString();

    [BsonElement("assignedModel")]
    public string AssignedModel { get; set; } = string.Empty;

    [BsonElement("routingTier")]
    public string RoutingTier { get; set; } = AiRoutingTier.Fast.ToString();

    [BsonElement("affinityLockedAtUtc")]
    public DateTime AffinityLockedAtUtc { get; set; }

    [BsonElement("markdownVersion")]
    public string MarkdownVersion { get; set; } = "1.0.0";

    [BsonElement("lastScreenContext")]
    public string? LastScreenContext { get; set; }

    [BsonElement("messageCount")]
    public int MessageCount { get; set; }

    [BsonElement("createdAtUtc")]
    public DateTime CreatedAtUtc { get; set; }

    [BsonElement("lastModifiedAtUtc")]
    public DateTime? LastModifiedAtUtc { get; set; }

    public static AiConversationDocument FromDomain(AiConversation entity) => new()
    {
        Id = entity.Id,
        OwnerUserId = entity.OwnerUserId,
        OwnerRole = entity.OwnerRole,
        Status = entity.Status.ToString(),
        AssignedModel = entity.AssignedModel,
        RoutingTier = entity.RoutingTier.ToString(),
        AffinityLockedAtUtc = entity.AffinityLockedAtUtc,
        MarkdownVersion = entity.MarkdownVersion,
        LastScreenContext = entity.LastScreenContext,
        MessageCount = entity.MessageCount,
        CreatedAtUtc = entity.CreatedAtUtc,
        LastModifiedAtUtc = entity.LastModifiedAtUtc
    };

    public AiConversation ToDomain()
    {
        var status = Enum.TryParse<AiConversationStatus>(Status, out var st) ? st : AiConversationStatus.Active;
        var tier = Enum.TryParse<AiRoutingTier>(RoutingTier, out var t) ? t : AiRoutingTier.Fast;

        return AiConversation.Reconstitute(
            Id,
            OwnerUserId,
            OwnerRole,
            status,
            AssignedModel,
            tier,
            AffinityLockedAtUtc,
            MarkdownVersion,
            LastScreenContext,
            MessageCount,
            CreatedAtUtc,
            LastModifiedAtUtc);
    }
}
