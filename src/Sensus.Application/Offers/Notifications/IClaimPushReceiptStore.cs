namespace Sensus.Application.Offers.Notifications;

public interface IClaimPushReceiptStore
{
    Task<bool> TryReserveAsync(Guid claimId, Guid deviceId, DateTime expiresAtUtc, CancellationToken cancellationToken);
}
