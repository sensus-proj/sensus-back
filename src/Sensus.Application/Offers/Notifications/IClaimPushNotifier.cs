using Sensus.Domain.Entities;

namespace Sensus.Application.Offers.Notifications;

public interface IClaimPushNotifier
{
    Task NotifyAsync(OfferClaim claim, CancellationToken cancellationToken);
}
