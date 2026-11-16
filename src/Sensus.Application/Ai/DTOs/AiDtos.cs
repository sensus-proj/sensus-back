namespace Sensus.Application.Ai.DTOs;

public sealed record CreateConversationRequestDto(
    bool ForceNew = false,
    string? ScreenContext = null);

public sealed record ConversationSummaryResponseDto(
    string ConversationId,
    string Status,
    string AssignedModel,
    string RoutingTier,
    int MessageCount,
    string? LastScreenContext,
    string MarkdownVersion,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);

public sealed record ConversationDetailResponseDto(
    ConversationSummaryResponseDto Conversation,
    IReadOnlyList<ConversationMessageItemDto> Messages,
    PaginationMetadataDto Pagination);

public sealed record ConversationMessageItemDto(
    string MessageId,
    string Role,
    string Content,
    RecommendedPackageDto? RecommendedPackage,
    IReadOnlyList<CitedOfferDto>? OffersCited,
    IReadOnlyList<string>? ToolsInvoked,
    bool Degraded,
    DateTime CreatedAtUtc);

public sealed record SendAiMessageRequestDto(
    string? ConversationId,
    string Message,
    string? ScreenContext = null,
    double? Latitude = null,
    double? Longitude = null,
    double? RadiusKm = null);

public sealed record AiTurnResponseDto(
    string ConversationId,
    string MessageId,
    string Reply,
    RecommendedPackageDto? RecommendedPackage,
    IReadOnlyList<CitedOfferDto>? OffersCited,
    IReadOnlyList<string> ToolsInvoked,
    bool Degraded,
    string? DegradationReason,
    string RoutingTier,
    string MarkdownVersion,
    string CorrelationId,
    AiUsageSummaryDto Usage);

public sealed record RecommendedPackageDto(
    decimal BudgetMxn,
    decimal TotalPriceMxn,
    decimal SavingsMxn,
    IReadOnlyList<CitedOfferDto> Items);

public sealed record CitedOfferDto(
    string OfferId,
    string Title,
    string MerchantId,
    string MerchantName,
    decimal OfferPriceMxn,
    decimal RegularPriceMxn,
    int AvailableStock,
    bool IsOpenNow,
    double? DistanceKm = null,
    string? Description = null,
    int? DiscountPercentage = null,
    string? ImageUrl = null,
    string? Category = null,
    DateTime? EndsAtUtc = null,
    string? MerchantAddress = null,
    double? Latitude = null,
    double? Longitude = null);

public sealed record AiUsageSummaryDto(
    int PromptTokens,
    int CachedTokens,
    int CompletionTokens,
    bool CacheHit);

public sealed record PaginationMetadataDto(
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);
