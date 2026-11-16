using Sensus.Domain.Common;
using Sensus.Domain.Enums;

namespace Sensus.Domain.Entities;

public class AiTraceLog : BaseEntity
{
    public Guid ConversationId { get; private set; }
    public Guid OwnerUserId { get; private set; }
    public string CorrelationId { get; private set; } = string.Empty;
    public string MarkdownVersion { get; private set; } = "1.0.0";
    public string MarkdownHashSha256 { get; private set; } = string.Empty;
    public AiRoutingTier RoutingTier { get; private set; }
    public string ModelAliasInternal { get; private set; } = string.Empty;
    public string RedactedPrompt { get; private set; } = string.Empty;
    public string RedactedResponse { get; private set; } = string.Empty;
    public IReadOnlyList<string> ToolsInvokedSummary { get; private set; } = [];
    public int PromptTokens { get; private set; }
    public int CachedTokens { get; private set; }
    public int CompletionTokens { get; private set; }
    public long TotalLatencyMs { get; private set; }
    public decimal EstimatedCostUsd { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime ExpiresAtUtc { get; private set; }

    private AiTraceLog() : base()
    {
    }

    private AiTraceLog(
        Guid id,
        Guid conversationId,
        Guid ownerUserId,
        string correlationId,
        string markdownVersion,
        string markdownHashSha256,
        AiRoutingTier routingTier,
        string modelAliasInternal,
        string redactedPrompt,
        string redactedResponse,
        IReadOnlyList<string> toolsInvokedSummary,
        int promptTokens,
        int cachedTokens,
        int completionTokens,
        long totalLatencyMs,
        decimal estimatedCostUsd,
        DateTime createdAtUtc,
        DateTime expiresAtUtc) : base(id)
    {
        ConversationId = conversationId;
        OwnerUserId = ownerUserId;
        CorrelationId = correlationId;
        MarkdownVersion = markdownVersion;
        MarkdownHashSha256 = markdownHashSha256;
        RoutingTier = routingTier;
        ModelAliasInternal = modelAliasInternal;
        RedactedPrompt = redactedPrompt;
        RedactedResponse = redactedResponse;
        ToolsInvokedSummary = toolsInvokedSummary;
        PromptTokens = promptTokens;
        CachedTokens = cachedTokens;
        CompletionTokens = completionTokens;
        TotalLatencyMs = totalLatencyMs;
        EstimatedCostUsd = estimatedCostUsd;
        CreatedAtUtc = createdAtUtc;
        ExpiresAtUtc = expiresAtUtc;
    }

    public static Result<AiTraceLog> Create(
        Guid conversationId,
        Guid ownerUserId,
        string correlationId,
        string markdownVersion,
        string markdownHashSha256,
        AiRoutingTier routingTier,
        string modelAliasInternal,
        string redactedPrompt,
        string redactedResponse,
        IEnumerable<string>? toolsInvokedSummary,
        int promptTokens,
        int cachedTokens,
        int completionTokens,
        long totalLatencyMs,
        decimal estimatedCostUsd,
        DateTime createdAtUtc,
        TimeSpan retentionPeriod)
    {
        if (conversationId == Guid.Empty)
        {
            return Result.Failure<AiTraceLog>(
                Error.Validation("Ai.InvalidTraceConversation", "El identificador de conversación es obligatorio para la traza."));
        }

        if (ownerUserId == Guid.Empty)
        {
            return Result.Failure<AiTraceLog>(
                Error.Validation("Ai.InvalidTraceOwner", "El identificador de usuario es obligatorio para la traza."));
        }

        var log = new AiTraceLog(
            Guid.NewGuid(),
            conversationId,
            ownerUserId,
            correlationId.Trim(),
            markdownVersion.Trim(),
            markdownHashSha256.Trim(),
            routingTier,
            modelAliasInternal.Trim(),
            redactedPrompt.Trim(),
            redactedResponse.Trim(),
            (toolsInvokedSummary ?? []).ToList().AsReadOnly(),
            Math.Max(0, promptTokens),
            Math.Max(0, cachedTokens),
            Math.Max(0, completionTokens),
            Math.Max(0, totalLatencyMs),
            Math.Max(0, estimatedCostUsd),
            createdAtUtc,
            createdAtUtc.Add(retentionPeriod <= TimeSpan.Zero ? TimeSpan.FromDays(30) : retentionPeriod));

        return Result.Success(log);
    }

    public static AiTraceLog Reconstitute(
        Guid id,
        Guid conversationId,
        Guid ownerUserId,
        string correlationId,
        string markdownVersion,
        string markdownHashSha256,
        AiRoutingTier routingTier,
        string modelAliasInternal,
        string redactedPrompt,
        string redactedResponse,
        IReadOnlyList<string> toolsInvokedSummary,
        int promptTokens,
        int cachedTokens,
        int completionTokens,
        long totalLatencyMs,
        decimal estimatedCostUsd,
        DateTime createdAtUtc,
        DateTime expiresAtUtc)
    {
        return new AiTraceLog(
            id,
            conversationId,
            ownerUserId,
            correlationId,
            markdownVersion,
            markdownHashSha256,
            routingTier,
            modelAliasInternal,
            redactedPrompt,
            redactedResponse,
            toolsInvokedSummary,
            promptTokens,
            cachedTokens,
            completionTokens,
            totalLatencyMs,
            estimatedCostUsd,
            createdAtUtc,
            expiresAtUtc);
    }
}
