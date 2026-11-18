using Sensus.Domain.Common;
using Sensus.Domain.Enums;
using Sensus.Domain.ValueObjects;

namespace Sensus.Domain.Entities;

public class AiMessage : BaseEntity
{
    public Guid ConversationId { get; private set; }
    public Guid OwnerUserId { get; private set; }
    public AiMessageRole Role { get; private set; }
    public string RedactedContent { get; private set; } = string.Empty;
    public bool Degraded { get; private set; }
    public string? DegradationReason { get; private set; }
    public RecommendedPackage? RecommendedPackage { get; private set; }
    public IReadOnlyList<string> OffersCitedIds { get; private set; } = [];
    public IReadOnlyList<string> ToolsInvoked { get; private set; } = [];
    public DateTime CreatedAtUtc { get; private set; }

    private AiMessage() : base()
    {
    }

    private AiMessage(
        Guid id,
        Guid conversationId,
        Guid ownerUserId,
        AiMessageRole role,
        string redactedContent,
        bool degraded,
        string? degradationReason,
        RecommendedPackage? recommendedPackage,
        IReadOnlyList<string> offersCitedIds,
        IReadOnlyList<string> toolsInvoked,
        DateTime createdAtUtc) : base(id)
    {
        ConversationId = conversationId;
        OwnerUserId = ownerUserId;
        Role = role;
        RedactedContent = redactedContent;
        Degraded = degraded;
        DegradationReason = degradationReason;
        RecommendedPackage = recommendedPackage;
        OffersCitedIds = offersCitedIds;
        ToolsInvoked = toolsInvoked;
        CreatedAtUtc = createdAtUtc;
    }

    public static Result<AiMessage> Create(
        Guid conversationId,
        Guid ownerUserId,
        AiMessageRole role,
        string redactedContent,
        bool degraded,
        string? degradationReason,
        RecommendedPackage? recommendedPackage,
        IEnumerable<string>? offersCitedIds,
        IEnumerable<string>? toolsInvoked,
        DateTime createdAtUtc)
    {
        if (conversationId == Guid.Empty)
        {
            return Result.Failure<AiMessage>(
                Error.Validation("Ai.InvalidConversationId", "El identificador de conversación es obligatorio."));
        }

        if (ownerUserId == Guid.Empty)
        {
            return Result.Failure<AiMessage>(
                Error.Validation("Ai.InvalidOwnerUserId", "El identificador de usuario es obligatorio."));
        }

        if (string.IsNullOrWhiteSpace(redactedContent))
        {
            return Result.Failure<AiMessage>(
                Error.Validation("Ai.EmptyMessageContent", "El contenido del mensaje no puede estar vacío."));
        }

        var message = new AiMessage(
            Guid.NewGuid(),
            conversationId,
            ownerUserId,
            role,
            redactedContent.Trim(),
            degraded,
            degradationReason,
            recommendedPackage,
            (offersCitedIds ?? []).ToList().AsReadOnly(),
            (toolsInvoked ?? []).ToList().AsReadOnly(),
            createdAtUtc);

        return Result.Success(message);
    }

    public static AiMessage Reconstitute(
        Guid id,
        Guid conversationId,
        Guid ownerUserId,
        AiMessageRole role,
        string redactedContent,
        bool degraded,
        string? degradationReason,
        RecommendedPackage? recommendedPackage,
        IReadOnlyList<string> offersCitedIds,
        IReadOnlyList<string> toolsInvoked,
        DateTime createdAtUtc)
    {
        return new AiMessage(
            id,
            conversationId,
            ownerUserId,
            role,
            redactedContent,
            degraded,
            degradationReason,
            recommendedPackage,
            offersCitedIds,
            toolsInvoked,
            createdAtUtc);
    }
}
