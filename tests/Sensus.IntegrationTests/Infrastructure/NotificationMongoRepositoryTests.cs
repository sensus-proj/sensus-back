using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Sensus.Domain.Entities;
using Sensus.Infrastructure.Persistence.Mongo;
using Sensus.Infrastructure.Persistence.Mongo.Repositories;
using Testcontainers.MongoDb;

namespace Sensus.IntegrationTests.Infrastructure;

public sealed class NotificationMongoRepositoryTests : IAsyncLifetime
{
    private readonly MongoDbContainer _container = new MongoDbBuilder("mongo:7.0").Build();
    private MongoDbContext _context = null!;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        _context = new MongoDbContext(
            Options.Create(new MongoDbSettings { ConnectionString = _container.GetConnectionString() }),
            NullLogger<MongoDbContext>.Instance);
    }

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    [Fact]
    public async Task OfferClaim_LastUnitAndActiveClaimAreUniqueUnderConcurrency()
    {
        var now = new DateTime(2026, 11, 22, 12, 0, 0, DateTimeKind.Utc);
        var offers = new MongoOfferRepository(_context, NullLogger<MongoOfferRepository>.Instance);
        var claims = new MongoOfferClaimRepository(_context, NullLogger<MongoOfferClaimRepository>.Instance);
        var offer = Offer.Create(Guid.NewGuid(), "Última unidad", "Disponible para recoger",
            100m, 10, 1, now.AddHours(-1), now.AddHours(1), nowUtc: now).Value;
        await offers.AddAsync(offer);

        var reservations = await Task.WhenAll(
            offers.TryReserveStockAsync(offer.Id, now),
            offers.TryReserveStockAsync(offer.Id, now));

        Assert.Single(reservations, reserved => reserved);
        Assert.Equal(0, (await offers.GetByIdAsync(offer.Id))!.Stock);

        var userId = Guid.NewGuid();
        var first = OfferClaim.Create(userId, offer.Id, offer.MerchantId, now).Value;
        var second = OfferClaim.Create(userId, offer.Id, offer.MerchantId, now).Value;
        Assert.True(first.AssignCodeHash(new string('a', 64)).IsSuccess);
        Assert.True(second.AssignCodeHash(new string('b', 64)).IsSuccess);

        var inserted = await Task.WhenAll(claims.TryAddAsync(first), claims.TryAddAsync(second));

        Assert.Single(inserted, added => added);
        Assert.Equal(1, await claims.GetActiveClaimCountAsync(offer.Id));
    }

    [Fact]
    public async Task NearbyMerchants_ReturnsStablePagesAndCountsOnlyActiveMerchantsInsideRadius()
    {
        var repository = new MongoComercioRepository(_context, NullLogger<MongoComercioRepository>.Instance);
        var nearest = Comercio.Create(Guid.NewGuid(), "Tienda Uno", "Calle Uno 1", "03100", 19.431, -99.133).Value;
        var second = Comercio.Create(Guid.NewGuid(), "Tienda Dos", "Calle Dos 2", "03100", 19.435, -99.133).Value;
        var inactive = Comercio.Create(Guid.NewGuid(), "Tienda Tres", "Calle Tres 3", "03100", 19.432, -99.133).Value;
        var distant = Comercio.Create(Guid.NewGuid(), "Tienda Cuatro", "Calle Cuatro 4", "03100", 19.471, -99.133).Value;
        inactive.Deactivate();
        await repository.AddAsync(second);
        await repository.AddAsync(inactive);
        await repository.AddAsync(nearest);
        await repository.AddAsync(distant);

        var firstPage = await repository.GetNearbyAsync(19.432, -99.133, 2_000, 1, 1);
        var secondPage = await repository.GetNearbyAsync(19.432, -99.133, 2_000, 2, 1);
        var beyondLastPage = await repository.GetNearbyAsync(19.432, -99.133, 2_000, int.MaxValue, 100);

        Assert.Equal(nearest.Id, Assert.Single(firstPage.Items).Id);
        Assert.Equal(second.Id, Assert.Single(secondPage.Items).Id);
        Assert.Empty(beyondLastPage.Items);
        Assert.Equal(2, firstPage.TotalCount);
        Assert.Equal(2, secondPage.TotalCount);
        Assert.Equal(2, beyondLastPage.TotalCount);
    }

    [Fact]
    public async Task ReserveClaimPush_AllowsOneConcurrentAttemptAndDeclaresExpiry()
    {
        var store = new MongoClaimPushReceiptStore(_context);
        var claimId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        DateTime expires = DateTime.UtcNow.AddHours(1);
        bool[] outcomes = await Task.WhenAll(Enumerable.Range(0, 5)
            .Select(_ => store.TryReserveAsync(claimId, deviceId, expires, CancellationToken.None)));

        Assert.Single(outcomes, accepted => accepted);
        using var indexes = await _context.ClaimPushReceipts.Indexes.ListAsync();
        Assert.Contains(await indexes.ToListAsync(), index => index.Contains("expireAfterSeconds"));
    }

    [Fact]
    public async Task SaveLocation_ReplacesPreviousLocationAndUsesGeospatialAndExpiryIndexes()
    {
        var store = new MongoNotificationLocationRepository(_context);
        var userId = Guid.NewGuid();
        DateTime now = DateTime.UtcNow;
        await store.SaveAsync(userId, 18, -98, now, now.AddHours(24), CancellationToken.None);
        await store.SaveAsync(userId, 19, -99, now.AddMinutes(1), now.AddHours(24), CancellationToken.None);
        await store.SaveAsync(userId, 20, -100, now, now.AddHours(24), CancellationToken.None);

        var location = await _context.NotificationLocations.Find(x => x.UserId == userId).SingleAsync();
        Assert.Equal(19, location.Coordinates.Coordinates.Latitude);
        Assert.Equal(-99, location.Coordinates.Coordinates.Longitude);
        using var indexes = await _context.NotificationLocations.Indexes.ListAsync();
        var definitions = await indexes.ToListAsync();
        Assert.Contains(definitions, index => index["key"].AsBsonDocument.TryGetValue("coordinates", out var value)
            && value.IsString && value.AsString == "2dsphere");
        Assert.Contains(definitions, index => index.Contains("expireAfterSeconds"));

        await store.DeleteAsync(userId, CancellationToken.None);
        await store.DeleteAsync(userId, CancellationToken.None);
        Assert.False(await _context.NotificationLocations.Find(x => x.UserId == userId).AnyAsync());
    }

    [Fact]
    public async Task GetActiveNear_ReturnsOnlyCurrentLocationsWithinDistanceOrderedByProximity()
    {
        var store = new MongoNotificationLocationRepository(_context);
        DateTime now = DateTime.UtcNow;
        Guid near = Guid.NewGuid(), farther = Guid.NewGuid(), outside = Guid.NewGuid(), expired = Guid.NewGuid();
        // Origen: Zócalo CDMX (19.4326, -99.1332). 0.01° de latitud ≈ 1.11 km.
        await store.SaveAsync(farther, 19.4596, -99.1332, now, now.AddHours(24), CancellationToken.None);
        await store.SaveAsync(near, 19.4336, -99.1332, now, now.AddHours(24), CancellationToken.None);
        await store.SaveAsync(outside, 19.6326, -99.1332, now, now.AddHours(24), CancellationToken.None);
        await store.SaveAsync(expired, 19.4326, -99.1332, now.AddHours(-25), now.AddSeconds(-1), CancellationToken.None);

        IReadOnlyList<Sensus.Domain.Repositories.NotificationLocationMatch> matches =
            await store.GetActiveNearAsync(19.4326, -99.1332, 10, now, 10, CancellationToken.None);

        Assert.Equal(new[] { near, farther }, matches.Select(m => m.UserId));
        Assert.InRange(matches[0].DistanceKm, 0.1, 0.12);
        Assert.InRange(matches[1].DistanceKm, 2.9, 3.1);
        Assert.Single(await store.GetActiveNearAsync(19.4326, -99.1332, 10, now, 1, CancellationToken.None));
    }

    [Fact]
    public async Task BatchReads_ReturnSettingsAndActiveDevicesForRequestedUsersOnly()
    {
        var settingsStore = new MongoNotificationSettingsRepository(_context, NullLogger<MongoNotificationSettingsRepository>.Instance);
        var deviceStore = new MongoDeviceRepository(_context, NullLogger<MongoDeviceRepository>.Instance);
        Guid first = Guid.NewGuid(), second = Guid.NewGuid(), other = Guid.NewGuid();
        await settingsStore.AddAsync(NotificationSettings.CreateDefaults(first));
        await settingsStore.AddAsync(NotificationSettings.CreateDefaults(other));
        Device active = Device.Create(second, "a", "Android", "batch-active-token", DateTime.UtcNow);
        Device inactive = Device.Create(second, "b", "Android", "batch-inactive-token", DateTime.UtcNow);
        await deviceStore.AddAsync(active);
        await deviceStore.AddAsync(inactive);
        await deviceStore.AddAsync(Device.Create(other, "c", "Android", "batch-other-token", DateTime.UtcNow));
        await deviceStore.DeactivateByTokenAsync(inactive.Id, inactive.Token);

        var settings = await settingsStore.GetByUserIdsAsync([first, second]);
        var devices = await deviceStore.GetActiveByUsersAsync([first, second]);

        Assert.Equal(first, Assert.Single(settings).UserId);
        Assert.Equal(active.Id, Assert.Single(devices).Id);
        Assert.Empty(await settingsStore.GetByUserIdsAsync([]));
    }

    [Fact]
    public async Task RemoveTokenBinding_AllowsNewOwnerAndRejectsStaleOwnership()
    {
        var store = new MongoDeviceRepository(_context, NullLogger<MongoDeviceRepository>.Instance);
        var previous = Device.Create(Guid.NewGuid(), "installation", "Android", "shared-installation-token", DateTime.UtcNow);
        var next = Device.Create(Guid.NewGuid(), previous.DeviceId, previous.Platform, previous.Token, DateTime.UtcNow);
        await store.AddAsync(previous);

        Assert.False(await store.TryRemoveTokenBindingAsync(previous.Id, next.UserId, previous.Token));
        Assert.True(await store.TryRemoveTokenBindingAsync(previous.Id, previous.UserId, previous.Token));
        await store.AddAsync(next);

        Assert.Empty(await store.GetActiveByUserAsync(previous.UserId));
        Assert.Equal(next.Id, Assert.Single(await store.GetActiveByUserAsync(next.UserId)).Id);
    }

    [Fact]
    public async Task DeactivateDevice_DoesNotOverwriteARotatedToken()
    {
        var store = new MongoDeviceRepository(_context, NullLogger<MongoDeviceRepository>.Instance);
        Device device = Device.Create(Guid.NewGuid(), "test", "Android", "first-test-device-token", DateTime.UtcNow);
        await store.AddAsync(device);
        string previousToken = device.Token;
        device.UpdateToken("second-test-device-token");
        await store.UpdateAsync(device);

        await store.DeactivateByTokenAsync(device.Id, previousToken);
        Assert.True((await store.GetByIdAsync(device.Id))!.IsActive);

        await store.DeactivateByTokenAsync(device.Id, device.Token);
        Assert.False((await store.GetByIdAsync(device.Id))!.IsActive);
    }
}
