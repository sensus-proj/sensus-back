namespace Sensus.Infrastructure.Persistence.Mongo;

public sealed class MongoDbSettings
{
    public const string SectionName = "MongoDbSettings";

    public string ConnectionString { get; set; } = string.Empty;
    public string DatabaseName { get; set; } = "SensusDb";
    public string UsersCollectionName { get; set; } = "usuarios";
    public string ComerciosCollectionName { get; set; } = "comercios";
    public string PasswordResetTokensCollectionName { get; set; } = "password_reset_tokens";
    public string OffersCollectionName { get; set; } = "offers";
    public string OfferClaimsCollectionName { get; set; } = "offer_claims";
    public string OfferViewsCollectionName { get; set; } = "offer_views";
    public string MediaAssetsCollectionName { get; set; } = "media_assets";
    public string DevicesCollectionName { get; set; } = "devices";
    public string NotificationSettingsCollectionName { get; set; } = "notification_settings";
    public string AiConversationsCollectionName { get; set; } = "ai_conversations";
    public string AiMessagesCollectionName { get; set; } = "ai_messages";
    public string AiTraceLogsCollectionName { get; set; } = "ai_trace_logs";
    public string FriendRequestsCollectionName { get; set; } = "friend_requests";
    public string SharedOffersCollectionName { get; set; } = "shared_offers";
    public string SanctionsCollectionName { get; set; } = "sanctions";
    public string ModerationAuditCollectionName { get; set; } = "moderation_audit";
    public string UserInterestsCollectionName { get; set; } = "user_interests";
    public string OfferInteractionsCollectionName { get; set; } = "offer_interactions";
    public string FeedPostsCollectionName { get; set; } = "feed_posts";
    public string FeedCommentsCollectionName { get; set; } = "feed_comments";
    public string ContentReportsCollectionName { get; set; } = "content_reports";
    public string OfferStatusHistoryCollectionName { get; set; } = "offer_status_history";
    public string OfferFavoritesCollectionName { get; set; } = "offer_favorites";
}
