using Sensus.Domain.Common;

namespace Sensus.Domain.Entities;

/// <summary>
/// Entidad de dominio que representa las preferencias de notificación de un usuario (Spec 044).
/// </summary>
public sealed class NotificationSettings : BaseEntity
{
    internal NotificationSettings(
        Guid id,
        Guid userId,
        bool pushEnabled,
        bool offerAlerts,
        bool socialAlerts,
        int radiusKm,
        DateTime createdAtUtc,
        DateTime? updatedAtUtc)
    {
        Id = id;
        UserId = userId;
        PushEnabled = pushEnabled;
        OfferAlerts = offerAlerts;
        SocialAlerts = socialAlerts;
        RadiusKm = radiusKm;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = updatedAtUtc;
    }

    public Guid UserId { get; private set; }
    public bool PushEnabled { get; private set; }
    public bool OfferAlerts { get; private set; }
    public bool SocialAlerts { get; private set; }
    public int RadiusKm { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? UpdatedAtUtc { get; private set; }

    public static NotificationSettings CreateDefaults(Guid userId)
    {
        var now = DateTime.UtcNow;
        return new NotificationSettings(
            Guid.NewGuid(),
            userId,
            pushEnabled: true,
            offerAlerts: true,
            socialAlerts: true,
            radiusKm: 5,
            createdAtUtc: now,
            updatedAtUtc: null);
    }

    public void Update(bool? pushEnabled, bool? offerAlerts, bool? socialAlerts, int? radiusKm)
    {
        if (pushEnabled.HasValue) PushEnabled = pushEnabled.Value;
        if (offerAlerts.HasValue) OfferAlerts = offerAlerts.Value;
        if (socialAlerts.HasValue) SocialAlerts = socialAlerts.Value;
        if (radiusKm.HasValue)
        {
            if (radiusKm.Value < 1 || radiusKm.Value > 50)
                throw new ArgumentOutOfRangeException(nameof(radiusKm), "El radio debe estar entre 1 y 50 km.");
            RadiusKm = radiusKm.Value;
        }
        UpdatedAtUtc = DateTime.UtcNow;
    }
}
