using Sensus.Domain.Common;
using Sensus.Domain.Enums;

namespace Sensus.Domain.Entities;

public class AiConversation : BaseAuditableEntity, IAggregateRoot
{
    public Guid OwnerUserId { get; private set; }
    public string OwnerRole { get; private set; } = RoleType.Consumidor;
    public AiConversationStatus Status { get; private set; }
    public string AssignedModel { get; private set; } = string.Empty;
    public AiRoutingTier RoutingTier { get; private set; }
    public DateTime AffinityLockedAtUtc { get; private set; }
    public string MarkdownVersion { get; private set; } = "1.0.0";
    public string? LastScreenContext { get; private set; }
    public int MessageCount { get; private set; }

    private AiConversation() : base()
    {
    }

    private AiConversation(
        Guid id,
        Guid ownerUserId,
        string ownerRole,
        string assignedModel,
        AiRoutingTier routingTier,
        string markdownVersion,
        string? screenContext,
        DateTime createdAtUtc) : base(id)
    {
        OwnerUserId = ownerUserId;
        OwnerRole = ownerRole;
        Status = AiConversationStatus.Active;
        AssignedModel = assignedModel;
        RoutingTier = routingTier;
        AffinityLockedAtUtc = createdAtUtc;
        MarkdownVersion = markdownVersion;
        LastScreenContext = screenContext;
        MessageCount = 0;
        CreatedAtUtc = createdAtUtc;
        LastModifiedAtUtc = createdAtUtc;
    }

    public DateTime? UpdatedAtUtc => LastModifiedAtUtc;

    public static Result<AiConversation> Create(
        Guid ownerUserId,
        string ownerRole,
        string assignedModel,
        AiRoutingTier routingTier,
        string markdownVersion,
        string? screenContext,
        DateTime createdAtUtc)
    {
        if (ownerUserId == Guid.Empty)
        {
            return Result.Failure<AiConversation>(
                Error.Validation("Ai.InvalidOwner", "El usuario propietario de la conversación es obligatorio."));
        }

        if (string.IsNullOrWhiteSpace(assignedModel))
        {
            return Result.Failure<AiConversation>(
                Error.Validation("Ai.InvalidModel", "El modelo asignado para afinidad de sesión no puede estar vacío."));
        }

        if (!string.Equals(ownerRole, RoleType.Consumidor, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(ownerRole, RoleType.Comerciante, StringComparison.OrdinalIgnoreCase))
        {
            return Result.Failure<AiConversation>(
                Error.Validation("Ai.InvalidRole", "El rol de la conversación debe ser Consumidor o Comerciante; los invitados y roles no reconocidos no son permitidos."));
        }

        var conversation = new AiConversation(
            Guid.NewGuid(),
            ownerUserId,
            ownerRole,
            assignedModel.Trim(),
            routingTier,
            string.IsNullOrWhiteSpace(markdownVersion) ? "1.0.0" : markdownVersion.Trim(),
            screenContext?.Trim(),
            createdAtUtc);

        return Result.Success(conversation);
    }

    public Result UpdateScreenContext(string? screenContext, DateTime updatedAtUtc)
    {
        LastScreenContext = screenContext?.Trim();
        LastModifiedAtUtc = updatedAtUtc;
        return Result.Success();
    }

    public Result IncrementMessageCount(DateTime updatedAtUtc)
    {
        MessageCount++;
        LastModifiedAtUtc = updatedAtUtc;
        return Result.Success();
    }

    public Result Archive(DateTime archivedAtUtc)
    {
        Status = AiConversationStatus.Archived;
        LastModifiedAtUtc = archivedAtUtc;
        return Result.Success();
    }

    public static AiConversation Reconstitute(
        Guid id,
        Guid ownerUserId,
        string ownerRole,
        AiConversationStatus status,
        string assignedModel,
        AiRoutingTier routingTier,
        DateTime affinityLockedAtUtc,
        string markdownVersion,
        string? screenContext,
        int messageCount,
        DateTime createdAtUtc,
        DateTime? lastModifiedAtUtc)
    {
        return new AiConversation(
            id,
            ownerUserId,
            ownerRole,
            assignedModel,
            routingTier,
            markdownVersion,
            screenContext,
            createdAtUtc)
        {
            Status = status,
            AffinityLockedAtUtc = affinityLockedAtUtc,
            MessageCount = messageCount,
            LastModifiedAtUtc = lastModifiedAtUtc
        };
    }
}
