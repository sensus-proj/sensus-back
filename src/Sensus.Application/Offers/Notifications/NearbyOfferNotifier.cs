using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sensus.Application.Common.Interfaces;
using Sensus.Domain.Entities;
using Sensus.Domain.Repositories;

namespace Sensus.Application.Offers.Notifications;

/// <summary>
/// FR-044-08: avisa "Nuevo {título} cerca de ti" a los usuarios cercanos cuando se crea una oferta disponible.
/// </summary>
public interface INearbyOfferNotifier
{
    /// <summary>Nunca lanza: un fallo del aviso no debe afectar la creación de la oferta.</summary>
    Task NotifyAsync(Offer offer, Guid merchantUserId, CancellationToken cancellationToken);
}

public sealed class NearbyOfferAlertSettings
{
    public const string SectionName = "NearbyOfferAlerts";

    public bool Enabled { get; init; } = true;
    public int TimeoutSeconds { get; init; } = 3;
    public double MaxRadiusKm { get; init; } = 50;
    public int MaxRecipients { get; init; } = 500;
    public int MaxParallelSends { get; init; } = 8;
    public int MaxTitleLength { get; init; } = 60;
}

public sealed class NearbyOfferNotifier(
    ITextModerationService moderation,
    INotificationLocationRepository locations,
    INotificationSettingsRepository preferences,
    IDeviceRepository devices,
    IPushDispatcher dispatcher,
    IDateTimeProvider clock,
    IOptions<NearbyOfferAlertSettings> options,
    ILogger<NearbyOfferNotifier> logger) : INearbyOfferNotifier
{
    public const string PushType = "nearby_offer";

    public async Task NotifyAsync(Offer offer, Guid merchantUserId, CancellationToken cancellationToken)
    {
        NearbyOfferAlertSettings settings = options.Value;
        DateTime now = clock.UtcNow;
        if (!settings.Enabled || offer.Latitude is not double latitude || offer.Longitude is not double longitude ||
            !offer.IsActive(now))
        {
            return;
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(settings.TimeoutSeconds));
        try
        {
            // Fail-closed: solo "Approved" habilita el aviso. Error, timeout o cuarentena => cero avisos.
            TextModerationResult verdict = await moderation.ModerateTextAsync(
                $"{offer.Title}\n{offer.Description}", timeout.Token);
            logger.LogInformation(
                "Nearby offer alert moderation for {OfferId}: {Decision} ({Categories}). Action: {Action}",
                offer.Id, verdict.Decision, string.Join(",", verdict.FlaggedCategories ?? []),
                verdict.IsApproved ? "notify" : "skip");
            if (!verdict.IsApproved)
            {
                return;
            }

            IReadOnlyList<NotificationLocationMatch> nearby = await locations.GetActiveNearAsync(
                latitude, longitude, settings.MaxRadiusKm, now, settings.MaxRecipients, timeout.Token);
            List<NotificationLocationMatch> candidates = nearby.Where(m => m.UserId != merchantUserId).ToList();
            if (candidates.Count == 0)
            {
                return;
            }

            Dictionary<Guid, NotificationSettings> saved = (await preferences.GetByUserIdsAsync(
                    candidates.Select(m => m.UserId).ToList(), timeout.Token))
                .ToDictionary(p => p.UserId);
            List<Guid> recipients = candidates
                .Where(m => WantsAlert(saved.GetValueOrDefault(m.UserId) ?? NotificationSettings.CreateDefaults(m.UserId), m))
                .Select(m => m.UserId)
                .ToList();
            if (recipients.Count == 0)
            {
                return;
            }

            List<Device> targets = await devices.GetActiveByUsersAsync(recipients, timeout.Token);
            string title = $"Nuevo {Shorten(offer.Title, settings.MaxTitleLength)} cerca de ti";
            var data = new Dictionary<string, string>
            {
                ["type"] = PushType,
                ["offerId"] = offer.Id.ToString()
            };

            int accepted = 0;
            await Parallel.ForEachAsync(targets,
                new ParallelOptions { MaxDegreeOfParallelism = settings.MaxParallelSends, CancellationToken = timeout.Token },
                async (device, token) =>
                {
                    PushResult result = await dispatcher.SendAsync(device.Token, title, string.Empty, data, token);
                    if (result == PushResult.Accepted)
                    {
                        Interlocked.Increment(ref accepted);
                    }
                    else if (result == PushResult.InvalidToken)
                    {
                        await devices.DeactivateByTokenAsync(device.Id, device.Token, token);
                    }
                });

            logger.LogInformation("Nearby offer alert for {OfferId}: {Accepted}/{Devices} devices accepted",
                offer.Id, accepted, targets.Count);
        }
        catch (Exception exception)
        {
            logger.LogWarning("Nearby offer alert interrupted for {OfferId} ({ErrorType})",
                offer.Id, exception.GetType().Name);
        }
    }

    private static bool WantsAlert(NotificationSettings settings, NotificationLocationMatch match) =>
        settings.PushEnabled && settings.OfferAlerts && match.DistanceKm <= settings.RadiusKm;

    private static string Shorten(string text, int maxLength)
    {
        string trimmed = text.Trim();
        return trimmed.Length <= maxLength ? trimmed : string.Concat(trimmed.AsSpan(0, maxLength - 1).TrimEnd(), "…");
    }
}
