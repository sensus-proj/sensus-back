using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Offers.Notifications;
using Sensus.Domain.Entities;
using Sensus.Domain.Repositories;

namespace Sensus.Application.UnitTests.Offers;

public sealed class ClaimPushNotifierTests
{
    [Theory]
    [InlineData(true, true)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task NotifyAsync_UsesRecipientPreferencesAndReservesBeforeSending(bool buyerEnabled, bool merchantEnabled)
    {
        DateTime now = new(2026, 11, 18, 12, 0, 0, DateTimeKind.Utc);
        var buyerId = Guid.NewGuid();
        var merchantId = Guid.NewGuid();
        Comercio merchant = Comercio.Create(merchantId, "Comercio Test", "Calle 123", "06000", 19.4, -99.1).Value;
        OfferClaim claim = OfferClaim.Create(buyerId, Guid.NewGuid(), merchant.Id, now).Value;
        Device buyerDevice = Device.Create(buyerId, "buyer", "Android", "buyer-test-device", now);
        Device merchantDevice = Device.Create(merchantId, "merchant", "Android", "merchant-test-device", now);
        var merchants = Substitute.For<IComercioRepository>();
        var preferences = Substitute.For<INotificationSettingsRepository>();
        var devices = Substitute.For<IDeviceRepository>();
        var receipts = Substitute.For<IClaimPushReceiptStore>();
        var dispatcher = Substitute.For<IPushDispatcher>();

        merchants.GetByIdAsync(merchant.Id, Arg.Any<CancellationToken>()).Returns(merchant);
        RegisterRecipient(buyerDevice, buyerEnabled);
        RegisterRecipient(merchantDevice, merchantEnabled);
        var notifier = new ClaimPushNotifier(
            merchants, preferences, devices, receipts, dispatcher, NullLogger<ClaimPushNotifier>.Instance);

        await notifier.NotifyAsync(claim, CancellationToken.None);
        await notifier.NotifyAsync(claim, CancellationToken.None);

        string expiry = claim.ExpiresAtUtc.ToString("O");
        await dispatcher.Received(buyerEnabled ? 1 : 0).SendAsync(
            buyerDevice.Token, "Apartado confirmado", Arg.Any<string>(),
            Arg.Is<IReadOnlyDictionary<string, string>>(d =>
                d["type"] == "claim_confirmed" && d["expiresAtUtc"] == expiry && d.Count == 4),
            Arg.Any<CancellationToken>());
        await dispatcher.Received(merchantEnabled ? 1 : 0).SendAsync(
            merchantDevice.Token, "Apartaron una de tus ofertas", Arg.Any<string>(),
            Arg.Is<IReadOnlyDictionary<string, string>>(d => d["type"] == "merchant_offer_claimed" && d.Count == 3),
            Arg.Any<CancellationToken>());

        void RegisterRecipient(Device device, bool enabled)
        {
            var settings = NotificationSettings.CreateDefaults(device.UserId);
            settings.Update(enabled, null, null, null);
            preferences.GetByUserIdAsync(device.UserId, Arg.Any<CancellationToken>()).Returns(settings);
            devices.GetActiveByUserAsync(device.UserId, Arg.Any<CancellationToken>()).Returns([device]);
            receipts.TryReserveAsync(claim.Id, device.Id, claim.ExpiresAtUtc, Arg.Any<CancellationToken>())
                .Returns(true, false);
        }
    }

    [Fact]
    public async Task NotifyAsync_ContainsAdapterFailuresAndDeactivatesRejectedTokens()
    {
        DateTime now = new(2026, 11, 18, 12, 0, 0, DateTimeKind.Utc);
        var buyerId = Guid.NewGuid();
        var merchantUserId = Guid.NewGuid();
        Comercio merchant = Comercio.Create(merchantUserId, "Comercio Test", "Calle 123", "06000", 19.4, -99.1).Value;
        OfferClaim claim = OfferClaim.Create(buyerId, Guid.NewGuid(), merchant.Id, now).Value;
        Device buyerDevice = Device.Create(buyerId, "buyer", "Android", "buyer-rejected-device", now);
        Device merchantDevice = Device.Create(merchantUserId, "merchant", "Android", "merchant-failing-device", now);
        var merchants = Substitute.For<IComercioRepository>();
        var preferences = Substitute.For<INotificationSettingsRepository>();
        var devices = Substitute.For<IDeviceRepository>();
        var receipts = Substitute.For<IClaimPushReceiptStore>();
        var dispatcher = Substitute.For<IPushDispatcher>();

        merchants.GetByIdAsync(merchant.Id, Arg.Any<CancellationToken>()).Returns(merchant);
        devices.GetActiveByUserAsync(buyerId, Arg.Any<CancellationToken>()).Returns([buyerDevice]);
        devices.GetActiveByUserAsync(merchantUserId, Arg.Any<CancellationToken>()).Returns([merchantDevice]);
        receipts.TryReserveAsync(claim.Id, Arg.Any<Guid>(), claim.ExpiresAtUtc, Arg.Any<CancellationToken>())
            .Returns(true);
        dispatcher.SendAsync(buyerDevice.Token, Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<CancellationToken>())
            .Returns(PushResult.InvalidToken);
        dispatcher.SendAsync(merchantDevice.Token, Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<CancellationToken>())
            .Returns<PushResult>(_ => throw new HttpRequestException("unavailable"));
        var notifier = new ClaimPushNotifier(
            merchants, preferences, devices, receipts, dispatcher, NullLogger<ClaimPushNotifier>.Instance);

        Func<Task> act = () => notifier.NotifyAsync(claim, CancellationToken.None);

        await act.Should().NotThrowAsync();
        await devices.Received(1).DeactivateByTokenAsync(buyerDevice.Id, buyerDevice.Token, Arg.Any<CancellationToken>());
        await devices.DidNotReceive().DeactivateByTokenAsync(merchantDevice.Id, Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}
