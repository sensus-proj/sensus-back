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
}
