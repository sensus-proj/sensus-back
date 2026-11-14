using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using Sensus.Domain.Entities;

namespace Sensus.Infrastructure.Persistence.Mongo.Documents;

/// <summary>
/// Documento de preferencias de notificación en MongoDB Atlas (Spec 044).
/// </summary>
public sealed class NotificationSettingsDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public Guid Id { get; set; }

    [BsonElement("userId")]
    [BsonRepresentation(BsonType.String)]
    public Guid UserId { get; set; }

    [BsonElement("pushEnabled")]
    public bool PushEnabled { get; set; }

    [BsonElement("offerAlerts")]
    public bool OfferAlerts { get; set; }

    [BsonElement("socialAlerts")]
    public bool SocialAlerts { get; set; }

    [BsonElement("radiusKm")]
    public int RadiusKm { get; set; }

    [BsonElement("createdAtUtc")]
    public DateTime CreatedAtUtc { get; set; }

    [BsonElement("updatedAtUtc")]
    public DateTime? UpdatedAtUtc { get; set; }

    public static NotificationSettingsDocument FromDomain(NotificationSettings settings)
    {
        return new NotificationSettingsDocument
        {
            Id = settings.Id,
            UserId = settings.UserId,
            PushEnabled = settings.PushEnabled,
            OfferAlerts = settings.OfferAlerts,
            SocialAlerts = settings.SocialAlerts,
            RadiusKm = settings.RadiusKm,
            CreatedAtUtc = settings.CreatedAtUtc,
            UpdatedAtUtc = settings.UpdatedAtUtc
        };
    }

    public NotificationSettings ToDomain()
    {
        var settings = NotificationSettings.CreateDefaults(UserId);

        // Restaurar valores persistidos
        typeof(NotificationSettings).GetProperty("Id")?.SetValue(settings, Id);
        typeof(NotificationSettings).GetProperty("PushEnabled")?.SetValue(settings, PushEnabled);
        typeof(NotificationSettings).GetProperty("OfferAlerts")?.SetValue(settings, OfferAlerts);
        typeof(NotificationSettings).GetProperty("SocialAlerts")?.SetValue(settings, SocialAlerts);
        typeof(NotificationSettings).GetProperty("RadiusKm")?.SetValue(settings, RadiusKm);
        typeof(NotificationSettings).GetProperty("CreatedAtUtc")?.SetValue(settings, CreatedAtUtc);
        if (UpdatedAtUtc.HasValue)
        {
            typeof(NotificationSettings).GetProperty("UpdatedAtUtc")?.SetValue(settings, UpdatedAtUtc.Value);
        }

        return settings;
    }
}
