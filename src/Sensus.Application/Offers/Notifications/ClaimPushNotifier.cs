using System.Globalization;
using Microsoft.Extensions.Logging;
using Sensus.Application.Common.Interfaces;
using Sensus.Domain.Entities;
using Sensus.Domain.Repositories;

namespace Sensus.Application.Offers.Notifications;

public sealed class ClaimPushNotifier(
    IComercioRepository merchants,
    INotificationSettingsRepository preferences,
    IDeviceRepository devices,
    IClaimPushReceiptStore receipts,
    IPushDispatcher dispatcher,
    ILogger<ClaimPushNotifier> logger) : IClaimPushNotifier
{
    public async Task NotifyAsync(OfferClaim claim, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(2));

        await Task.WhenAll(
            NotifyRecipientAsync(claim, claim.UserId, true, timeout.Token),
            NotifyMerchantAsync(claim, timeout.Token));
    }

    private async Task NotifyMerchantAsync(OfferClaim claim, CancellationToken cancellationToken)
    {
        try
        {
            Comercio? merchant = await merchants.GetByIdAsync(claim.MerchantId, cancellationToken);
            if (merchant is { Activo: true } && merchant.UsuarioId != claim.UserId)
            {
                await NotifyRecipientAsync(claim, merchant.UsuarioId, false, cancellationToken);
            }
        }
        catch (Exception exception)
        {
            logger.LogWarning("Unable to resolve merchant for claim push ({ErrorType})", exception.GetType().Name);
        }
    }

    private async Task NotifyRecipientAsync(OfferClaim claim, Guid userId, bool buyer, CancellationToken cancellationToken)
    {
        try
        {
            NotificationSettings? settings = await preferences.GetByUserIdAsync(userId, cancellationToken);
            if (settings?.PushEnabled == false)
            {
                return;
            }

            string title = buyer ? "Apartado confirmado" : "Apartaron una de tus ofertas";
            string body = buyer
                ? $"Puedes recogerlo hasta {claim.ExpiresAtUtc.ToString("dd/MM HH:mm", CultureInfo.InvariantCulture)} UTC."
                : "Abre Sensus para consultar el apartado.";
            var data = new Dictionary<string, string>
            {
                ["type"] = buyer ? "claim_confirmed" : "merchant_offer_claimed",
                ["claimId"] = claim.Id.ToString(),
                ["offerId"] = claim.OfferId.ToString()
            };
            if (buyer)
            {
                data["expiresAtUtc"] = claim.ExpiresAtUtc.ToString("O", CultureInfo.InvariantCulture);
            }

            foreach (Device device in await devices.GetActiveByUserAsync(userId, cancellationToken))
            {
                if (!await receipts.TryReserveAsync(claim.Id, device.Id, claim.ExpiresAtUtc, cancellationToken))
                {
                    continue;
                }

                if (await dispatcher.SendAsync(device.Token, title, body, data, cancellationToken) == PushResult.InvalidToken)
                {
                    await devices.DeactivateByTokenAsync(device.Id, device.Token, cancellationToken);
                }
            }
        }
        catch (Exception exception)
        {
            logger.LogWarning("Claim push interrupted ({ErrorType})", exception.GetType().Name);
        }
    }
}
