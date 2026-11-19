using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Offers.Notifications;
using Sensus.Domain.Entities;
using Sensus.Domain.Repositories;

namespace Sensus.Application.UnitTests.Offers;

public sealed class NearbyOfferNotifierTests
{
    private static readonly DateTime Now = new(2026, 11, 19, 12, 0, 0, DateTimeKind.Utc);
    private readonly ITextModerationService _moderation = Substitute.For<ITextModerationService>();
    private readonly INotificationLocationRepository _locations = Substitute.For<INotificationLocationRepository>();
    private readonly INotificationSettingsRepository _preferences = Substitute.For<INotificationSettingsRepository>();
    private readonly IDeviceRepository _devices = Substitute.For<IDeviceRepository>();
    private readonly IPushDispatcher _dispatcher = Substitute.For<IPushDispatcher>();
    private readonly Guid _merchantUserId = Guid.NewGuid();
    private readonly NearbyOfferNotifier _notifier;

    public NearbyOfferNotifierTests()
    {
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(Now);
        _moderation.ModerateTextAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new TextModerationResult(true, Decision: "Approved"));
        _preferences.GetByUserIdsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new List<NotificationSettings>());
        _dispatcher.SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<CancellationToken>())
            .Returns(PushResult.Accepted);
        _notifier = new NearbyOfferNotifier(_moderation, _locations, _preferences, _devices, _dispatcher, clock,
            Options.Create(new NearbyOfferAlertSettings()), NullLogger<NearbyOfferNotifier>.Instance);
    }

    private static Offer BuildOffer(DateTime startsAt, string title = "Detergente") =>
        Offer.Create(Guid.NewGuid(), title, "Detergente líquido de 3 litros", 100m, 20, 10,
            startsAt, Now.AddDays(1), null, 19.4326, -99.1332, Now).Value;

    [Fact]
    public async Task NotifyAsync_SendsOnlyTitleToRecipientsWithinTheirRadiusAndEnabledAlerts()
    {
        Offer offer = BuildOffer(Now);
        Guid onEdge = Guid.NewGuid(), outside = Guid.NewGuid(), alertsOff = Guid.NewGuid(), pushOff = Guid.NewGuid();
        _locations.GetActiveNearAsync(19.4326, -99.1332, 50, Now, 500, Arg.Any<CancellationToken>())
            .Returns([
                new NotificationLocationMatch(onEdge, 5),
                new NotificationLocationMatch(outside, 5.01),
                new NotificationLocationMatch(alertsOff, 1),
                new NotificationLocationMatch(pushOff, 1),
                new NotificationLocationMatch(_merchantUserId, 0)
            ]);
        _preferences.GetByUserIdsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns([Settings(alertsOff, push: true, offers: false), Settings(pushOff, push: false, offers: true)]);
        Device device = Device.Create(onEdge, "phone", "Android", "nearby-device", Now);
        _devices.GetActiveByUsersAsync(Arg.Is<IReadOnlyCollection<Guid>>(ids => ids.SequenceEqual(new[] { onEdge })),
                Arg.Any<CancellationToken>())
            .Returns([device]);

        await _notifier.NotifyAsync(offer, _merchantUserId, CancellationToken.None);

        await _moderation.Received(1).ModerateTextAsync(
            "Detergente\nDetergente líquido de 3 litros", Arg.Any<CancellationToken>());
        await _preferences.Received(1).GetByUserIdsAsync(
            Arg.Is<IReadOnlyCollection<Guid>>(ids => !ids.Contains(_merchantUserId) && ids.Count == 4),
            Arg.Any<CancellationToken>());
        await _dispatcher.Received(1).SendAsync(
            "nearby-device", "Nuevo Detergente cerca de ti", string.Empty,
            Arg.Is<IReadOnlyDictionary<string, string>>(d =>
                d.Count == 2 && d["type"] == "nearby_offer" && d["offerId"] == offer.Id.ToString()),
            Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("Rejected")]
    [InlineData("Quarantine")]
    public async Task NotifyAsync_SendsNothingWhenTextIsNotApproved(string decision)
    {
        _moderation.ModerateTextAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new TextModerationResult(false, Decision: decision));

        await _notifier.NotifyAsync(BuildOffer(Now), _merchantUserId, CancellationToken.None);

        await _locations.DidNotReceiveWithAnyArgs().GetActiveNearAsync(default, default, default, default, default, default);
        await _dispatcher.DidNotReceiveWithAnyArgs().SendAsync(default!, default!, default!, default, default);
    }

    [Fact]
    public async Task NotifyAsync_ContainsModerationFailures()
    {
        _moderation.ModerateTextAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("unavailable"));

        Func<Task> act = () => _notifier.NotifyAsync(BuildOffer(Now), _merchantUserId, CancellationToken.None);

        await act.Should().NotThrowAsync();
        await _dispatcher.DidNotReceiveWithAnyArgs().SendAsync(default!, default!, default!, default, default);
    }

    [Fact]
    public async Task NotifyAsync_SkipsOffersScheduledForLater()
    {
        await _notifier.NotifyAsync(BuildOffer(Now.AddHours(2)), _merchantUserId, CancellationToken.None);

        await _moderation.DidNotReceiveWithAnyArgs().ModerateTextAsync(default!, default);
    }

    [Fact]
    public async Task NotifyAsync_ShortensLongTitlesAndDeactivatesRejectedTokens()
    {
        Offer offer = BuildOffer(Now, new string('a', 80));
        Guid userId = Guid.NewGuid();
        _locations.GetActiveNearAsync(default, default, default, default, default, default)
            .ReturnsForAnyArgs([new NotificationLocationMatch(userId, 1)]);
        Device device = Device.Create(userId, "phone", "Android", "stale-device", Now);
        _devices.GetActiveByUsersAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>()).Returns([device]);
        _dispatcher.SendAsync("stale-device", Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<CancellationToken>())
            .Returns(PushResult.InvalidToken);

        await _notifier.NotifyAsync(offer, _merchantUserId, CancellationToken.None);

        await _dispatcher.Received(1).SendAsync("stale-device",
            Arg.Is<string>(t => t == $"Nuevo {new string('a', 59)}… cerca de ti"), string.Empty,
            Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<CancellationToken>());
        await _devices.Received(1).DeactivateByTokenAsync(device.Id, "stale-device", Arg.Any<CancellationToken>());
    }

    private static NotificationSettings Settings(Guid userId, bool push, bool offers)
    {
        var settings = NotificationSettings.CreateDefaults(userId);
        settings.Update(push, offers, null, null);
        return settings;
    }
}
