namespace Sensus.Application.Users.DTOs;

public sealed record NotificationSettingsDto(
    Guid UserId,
    bool PushEnabled,
    bool OfferAlerts,
    bool SocialAlerts,
    int RadiusKm,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc = null);
