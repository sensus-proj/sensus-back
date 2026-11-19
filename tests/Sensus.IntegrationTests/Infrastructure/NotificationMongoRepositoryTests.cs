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
