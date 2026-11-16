using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using Sensus.Domain.Entities;
using Sensus.Domain.Enums;

namespace Sensus.Infrastructure.Persistence.Mongo.Documents;

public sealed class AiTraceLogDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public Guid Id { get; set; }

    [BsonRepresentation(BsonType.String)]
    [BsonElement("conversationId")]
    public Guid ConversationId { get; set; }

    [BsonRepresentation(BsonType.String)]
    [BsonElement("ownerUserId")]
    public Guid OwnerUserId { get; set; }

    [BsonElement("correlationId")]
    public string CorrelationId { get; set; } = string.Empty;

    [BsonElement("markdownVersion")]
    public string MarkdownVersion { get; set; } = "1.0.0";

    [BsonElement("markdownHashSha256")]
    public string MarkdownHashSha256 { get; set; } = string.Empty;

    [BsonElement("routingTier")]
    public string RoutingTier { get; set; } = AiRoutingTier.Fast.ToString();

    [BsonElement("modelAliasInternal")]
    public string ModelAliasInternal { get; set; } = string.Empty;

    [BsonElement("redactedPrompt")]
    public string RedactedPrompt { get; set; } = string.Empty;

    [BsonElement("redactedResponse")]
    public string RedactedResponse { get; set; } = string.Empty;

    [BsonElement("toolsInvokedSummary")]
    public List<string> ToolsInvokedSummary { get; set; } = new();

    [BsonElement("promptTokens")]
    public int PromptTokens { get; set; }

    [BsonElement("cachedTokens")]
    public int CachedTokens { get; set; }

    [BsonElement("completionTokens")]
    public int CompletionTokens { get; set; }

    [BsonElement("totalLatencyMs")]
    public long TotalLatencyMs { get; set; }

    [BsonElement("estimatedCostUsd")]
    [BsonRepresentation(BsonType.Decimal128)]
    public decimal EstimatedCostUsd { get; set; }

    [BsonElement("createdAtUtc")]
    public DateTime CreatedAtUtc { get; set; }

    [BsonElement("expiresAtUtc")]
    public DateTime ExpiresAtUtc { get; set; }

    public static AiTraceLogDocument FromDomain(AiTraceLog entity) => new()
    {
        Id = entity.Id,
        ConversationId = entity.ConversationId,
        OwnerUserId = entity.OwnerUserId,
        CorrelationId = entity.CorrelationId,
        MarkdownVersion = entity.MarkdownVersion,
        MarkdownHashSha256 = entity.MarkdownHashSha256,
        RoutingTier = entity.RoutingTier.ToString(),
        ModelAliasInternal = entity.ModelAliasInternal,
        RedactedPrompt = entity.RedactedPrompt,
        RedactedResponse = entity.RedactedResponse,
        ToolsInvokedSummary = entity.ToolsInvokedSummary.ToList(),
        PromptTokens = entity.PromptTokens,
        CachedTokens = entity.CachedTokens,
        CompletionTokens = entity.CompletionTokens,
        TotalLatencyMs = entity.TotalLatencyMs,
        EstimatedCostUsd = entity.EstimatedCostUsd,
        CreatedAtUtc = entity.CreatedAtUtc,
        ExpiresAtUtc = entity.ExpiresAtUtc
    };

    public AiTraceLog ToDomain()
    {
        var tier = Enum.TryParse<AiRoutingTier>(RoutingTier, out var t) ? t : AiRoutingTier.Fast;

        return AiTraceLog.Reconstitute(
            Id,
            ConversationId,
            OwnerUserId,
            CorrelationId,
            MarkdownVersion,
            MarkdownHashSha256,
            tier,
            ModelAliasInternal,
            RedactedPrompt,
            RedactedResponse,
            ToolsInvokedSummary.AsReadOnly(),
            PromptTokens,
            CachedTokens,
            CompletionTokens,
            TotalLatencyMs,
            EstimatedCostUsd,
            CreatedAtUtc,
            ExpiresAtUtc);
    }
}
