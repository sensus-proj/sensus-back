using Sensus.Domain.Enums;
using Sensus.Domain.ValueObjects;

namespace Sensus.Application.Common.Interfaces;

public sealed record KnowledgeBaseDocument(
    string Version,
    string HashSha256,
    string Content);

public interface IKnowledgeBaseProvider
{
    KnowledgeBaseDocument GetDocument();
}

public sealed record PromptInjectionCheckResult(
    bool IsSuspicious,
    string? Reason = null);

public interface IPromptInjectionDetector
{
    PromptInjectionCheckResult Check(string input);
}

public interface IPiiRedactor
{
    string Redact(string input);
}

public interface IHardRuleLeakDetector
{
    bool ContainsForbiddenLeaks(string outputText);
}

public sealed record AiRoutingDecision(
    string SelectedModel,
    AiRoutingTier Tier,
    string Reason);

public interface IAiModelRouter
{
    AiRoutingDecision RouteInitialTurn(string prompt, bool allowReasoning = true);
}

public interface IScopeAuthorizationMapper
{
    IReadOnlySet<AiScope> MapScopes(System.Security.Claims.ClaimsPrincipal user);
    IReadOnlySet<AiScope> MapScopesByRole(string? role);
}

public interface IAiBudgetGuard
{
    Task<bool> CanExecuteTurnAsync(Guid userId, decimal estimatedCostUsd, CancellationToken cancellationToken = default);
    Task<bool> IsReasoningBudgetAvailableAsync(CancellationToken cancellationToken = default);
    Task RecordSpendAsync(Guid userId, decimal actualCostUsd, AiRoutingTier routingTier = AiRoutingTier.Fast, CancellationToken cancellationToken = default);
    Task<int> GetTodayMessageCountAsync(Guid userId, CancellationToken cancellationToken = default);
}

public static class SensiToolNames
{
    public const string SearchOffers = "search_offers";
    public const string GetOfferDetail = "get_offer_detail";
    public const string GetMyAccount = "get_my_account";
    public const string GetMyClaims = "get_my_claims";
    public const string GetMyMerchantOffers = "get_my_merchant_offers";
}

public sealed record McpToolDefinition(
    string Name,
    string Description,
    object ParametersSchema,
    AiScope RequiredScope);

public sealed record McpToolCall(
    string Id,
    string ToolName,
    string ArgumentsJson);

public sealed record McpToolExecutionResult(
    string ToolCallId,
    string ToolName,
    string ResultJson,
    bool Success,
    string? ErrorMessage = null);

public interface IMcpToolRegistry
{
    IReadOnlyList<McpToolDefinition> GetAllowedTools(IReadOnlySet<AiScope> userScopes);
}

public interface IMcpToolExecutor
{
    Task<McpToolExecutionResult> ExecuteAsync(
        McpToolCall call,
        Guid callerUserId,
        IReadOnlySet<AiScope> callerScopes,
        CancellationToken cancellationToken = default);
}

public sealed record AiChatRequest(
    string Model,
    IReadOnlyList<McpToolDefinition> Tools,
    string SystemAndContextPrefix,
    string DynamicSessionContext,
    IReadOnlyList<AiMessageHistoryItem> RecentHistory,
    string UserPrompt);

public sealed record AiMessageHistoryItem(
    string Role,
    string Content);

public sealed record AiChatCompletionResult(
    string ReplyText,
    IReadOnlyList<McpToolCall> ToolCalls,
    int PromptTokens,
    int CachedTokens,
    int CompletionTokens,
    bool CacheHit);

public interface IAiChatGateway
{
    Task<AiChatCompletionResult> CompleteTurnAsync(
        AiChatRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record FallbackAiTurnResult(
    string Reply,
    RecommendedPackage? RecommendedPackage,
    IReadOnlyList<string> OffersCited,
    string DegradationReason);

public interface IDeterministicAiFallbackService
{
    Task<FallbackAiTurnResult> BuildFallbackAsync(
        string userPrompt,
        Guid userId,
        decimal? budgetMxn,
        CancellationToken cancellationToken = default);
}

public interface ICitedOfferEnricher
{
    /// <summary>
    /// Devuelve, con datos completos del repositorio, las ofertas citadas que la respuesta menciona por título
    /// (hasta <c>MaxCitedOfferCards</c>), excluyendo las que ya viajan en el paquete recomendado.
    /// </summary>
    Task<IReadOnlyList<Sensus.Application.Ai.DTOs.CitedOfferDto>> EnrichAsync(
        IReadOnlyList<Sensus.Application.Ai.DTOs.CitedOfferDto> citedOffers,
        string reply,
        IReadOnlySet<string> excludedOfferIds,
        double? latitude,
        double? longitude,
        CancellationToken cancellationToken = default);
}
