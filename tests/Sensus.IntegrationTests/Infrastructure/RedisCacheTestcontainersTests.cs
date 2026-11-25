using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Common.Settings;
using Sensus.Application.Offers;
using Sensus.Domain.Enums;
using Sensus.Domain.Models;
using Sensus.Infrastructure.Cache;
using StackExchange.Redis;
using Testcontainers.Redis;

namespace Sensus.IntegrationTests.Infrastructure;

/// <summary>
/// Spec 048, T048-17: pruebas contra Redis real. Usan <c>REDIS_CONNECTION_STRING</c> si el entorno la
/// proporciona; en caso contrario arrancan un contenedor Redis con Testcontainers. No hay dobles que
/// simulen el motor.
/// </summary>
public sealed class RedisCacheTestcontainersTests : IAsyncLifetime
{
    private readonly string? _externalConnectionString = Environment.GetEnvironmentVariable("REDIS_CONNECTION_STRING");
    private RedisContainer? _container;
    private ConnectionMultiplexer? _multiplexer;
    private RedisCacheService _cache = null!;
    private string _connectionString = string.Empty;

    public Task InitializeAsync() => InitializeCoreAsync();

    public async Task DisposeAsync()
    {
        _multiplexer?.Dispose();
        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }

    [Fact]
    public async Task RedisCacheService_ShouldRoundTripAndHonorConfiguredTtl()
    {
        string key = $"sensus:test:{Guid.NewGuid():N}";

        await _cache.SetAsync(key, new { Nombre = "Sensus" }, TimeSpan.FromSeconds(2));

        var value = await _cache.GetAsync<System.Text.Json.JsonElement>(key);
        value.GetProperty("Nombre").GetString().Should().Be("Sensus");

        await Task.Delay(TimeSpan.FromMilliseconds(2200));
        (await _cache.GetAsync<System.Text.Json.JsonElement>(key)).ValueKind
            .Should().Be(System.Text.Json.JsonValueKind.Undefined);
    }

    [Fact]
    public async Task RedisFeedSnapshotStore_ShouldIsolateByUserAndInvalidateAllSnapshotsByPrefix()
    {
        var store = new RedisFeedSnapshotStore(_cache, NullLogger<RedisFeedSnapshotStore>.Instance);
        Guid ownerId = Guid.NewGuid();
        Guid otherUserId = Guid.NewGuid();
        Guid snapshotId = Guid.NewGuid();
        var snapshot = new FeedSnapshot(
            snapshotId,
            "feed-v1",
            DateTime.UtcNow,
            [new FeedSnapshotEntry(Guid.NewGuid(), FeedSlot.Ranked), new FeedSnapshotEntry(Guid.NewGuid(), FeedSlot.Exploration)]);
        var otherSnapshot = new FeedSnapshot(
            Guid.NewGuid(),
            "feed-v1",
            DateTime.UtcNow,
            [new FeedSnapshotEntry(Guid.NewGuid(), FeedSlot.Ranked)]);

        (await store.SaveAsync(ownerId, snapshot, TimeSpan.FromMinutes(1))).Should().BeTrue();
        (await store.SaveAsync(otherUserId, otherSnapshot, TimeSpan.FromMinutes(1))).Should().BeTrue();
        (await store.GetAsync(ownerId, snapshotId))!.Entries.Should().HaveCount(2);

        // STRIDE: el mismo snapshotId no puede ser leído por otro usuario.
        (await store.GetAsync(otherUserId, snapshotId)).Should().BeNull();

        // FR-048-08: una mutación de oferta invalida todas las instantáneas del feed de ofertas.
        await _cache.RemoveByPrefixAsync(FeedCacheKeys.SnapshotPrefix);

        (await store.GetAsync(ownerId, snapshotId)).Should().BeNull();
        (await store.GetAsync(otherUserId, otherSnapshot.SnapshotId)).Should().BeNull();
    }

    private async Task InitializeCoreAsync()
    {
        if (string.IsNullOrWhiteSpace(_externalConnectionString))
        {
            _container = new RedisBuilder("redis:7-alpine").Build();
            await _container.StartAsync();
            _connectionString = _container.GetConnectionString();
        }
        else
        {
            _connectionString = _externalConnectionString;
        }

        _multiplexer = await ConnectionMultiplexer.ConnectAsync(_connectionString);
        _cache = new RedisCacheService(
            Options.Create(new RedisSettings { Enabled = true, DefaultTtlMinutes = 10 }),
            NullLogger<RedisCacheService>.Instance,
            _multiplexer);
    }
}
