using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Sensus.Api.Health;
using Sensus.Application.Common.Interfaces;
using Sensus.Infrastructure;
using Sensus.Infrastructure.Cache;
using Sensus.TestKit.Fakes;
using Xunit;

namespace Sensus.IntegrationTests.Infrastructure;

/// <summary>
/// Spec 048: caché en memoria de proceso (alternativa sin costo a ElastiCache), selección de proveedor por
/// configuración y health check de la caché (FR-048-12, NFR-048-04).
/// </summary>
public sealed class MemoryCacheServiceTests
{
    private sealed record Payload(string Name, int Value);

    private readonly FakeDateTimeProvider _clock = new(new DateTime(2026, 11, 19, 12, 0, 0, DateTimeKind.Utc));

    private MemoryCacheService Create(
        int maxEntries = 1000,
        int maxTtlSeconds = 60,
        bool enabled = true,
        RedisSettings? ttl = null) =>
        new(
            Options.Create(new MemoryCacheSettings { Enabled = enabled, MaxEntries = maxEntries, MaxTtlSeconds = maxTtlSeconds }),
            Options.Create(ttl ?? new RedisSettings()),
            _clock,
            NullLogger<MemoryCacheService>.Instance);

    [Fact]
    public async Task SetAndGet_RoundTripsTheValue()
    {
        var sut = Create();

        await sut.SetAsync("k", new Payload("a", 1), TimeSpan.FromSeconds(30));

        (await sut.GetAsync<Payload>("k")).Should().Be(new Payload("a", 1));
        (await sut.ExistsAsync("k")).Should().BeTrue();
        sut.Provider.Should().Be("memory");
        sut.IsAvailable.Should().BeTrue();
    }

    [Fact]
    public async Task Get_ReturnsACopy_SoCallersCannotMutateTheCachedValue()
    {
        var sut = Create();
        await sut.SetAsync("k", new List<int> { 1, 2 });

        var first = await sut.GetAsync<List<int>>("k");
        first!.Add(99);

        (await sut.GetAsync<List<int>>("k")).Should().Equal(1, 2);
    }

    [Fact]
    public async Task Get_AfterTheTtl_ReturnsNothingAndFreesTheEntry()
    {
        var sut = Create();
        await sut.SetAsync("k", new Payload("a", 1), TimeSpan.FromSeconds(30));

        _clock.Advance(TimeSpan.FromSeconds(31));

        (await sut.GetAsync<Payload>("k")).Should().BeNull();
        (await sut.ExistsAsync("k")).Should().BeFalse();
        sut.Count.Should().Be(0);
    }

    [Fact]
    public async Task Set_CapsTheTtlToMaxTtl_ToBoundStalenessAcrossInstances()
    {
        var sut = Create(maxTtlSeconds: 60);
        await sut.SetAsync("k", new Payload("a", 1), TimeSpan.FromMinutes(30));

        _clock.Advance(TimeSpan.FromSeconds(59));
        (await sut.GetAsync<Payload>("k")).Should().NotBeNull();

        _clock.Advance(TimeSpan.FromSeconds(2));
        (await sut.GetAsync<Payload>("k")).Should().BeNull();
    }

    [Fact]
    public async Task SetForEntity_UsesTheConfiguredEntityTtl_WhenShorterThanTheCap()
    {
        var sut = Create(maxTtlSeconds: 600, ttl: new RedisSettings { TtlByEntityMinutes = { ["Feed"] = 2 } });
        await sut.SetForEntityAsync("k", new Payload("a", 1), "Feed");

        _clock.Advance(TimeSpan.FromSeconds(61));
        (await sut.GetAsync<Payload>("k")).Should().NotBeNull();

        _clock.Advance(TimeSpan.FromSeconds(61));
        (await sut.GetAsync<Payload>("k")).Should().BeNull();
    }

    [Fact]
    public async Task Remove_And_RemoveByPrefix_InvalidateEntries()
    {
        var sut = Create();
        await sut.SetAsync("sensus:offers:1", new Payload("a", 1));
        await sut.SetAsync("sensus:offers:2", new Payload("b", 2));
        await sut.SetAsync("sensus:users:1", new Payload("c", 3));

        await sut.RemoveAsync("sensus:users:1");
        await sut.RemoveByPrefixAsync("sensus:offers:");

        sut.Count.Should().Be(0);
    }

    [Fact]
    public async Task WhenFull_PurgesExpiredEntriesFirst_ThenEvictsTheOldest()
    {
        var sut = Create(maxEntries: 10, maxTtlSeconds: 600);
        for (int i = 0; i < 10; i++)
        {
            await sut.SetAsync($"k{i}", new Payload("x", i), TimeSpan.FromSeconds(10 + i));
        }

        _clock.Advance(TimeSpan.FromSeconds(11)); // k0 y k1 vencieron
        await sut.SetAsync("nueva", new Payload("n", 1));
        sut.Count.Should().BeLessThanOrEqualTo(9);

        for (int i = 0; i < 30; i++)
        {
            await sut.SetAsync($"extra{i}", new Payload("e", i), TimeSpan.FromSeconds(500));
        }

        sut.Count.Should().BeLessThanOrEqualTo(10, "la caché nunca supera MaxEntries");
        (await sut.GetAsync<Payload>("extra29")).Should().NotBeNull();
    }

    [Fact]
    public async Task WhenDisabled_NothingIsStored_AndProviderIsNone()
    {
        var sut = Create(enabled: false);

        await sut.SetAsync("k", new Payload("a", 1));

        (await sut.GetAsync<Payload>("k")).Should().BeNull();
        sut.IsAvailable.Should().BeFalse();
        sut.Provider.Should().Be("none");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task BlankKeys_AreIgnored(string key)
    {
        var sut = Create();

        await sut.SetAsync(key, new Payload("a", 1));
        await sut.RemoveAsync(key);
        await sut.RemoveByPrefixAsync(key);

        sut.Count.Should().Be(0);
        (await sut.GetAsync<Payload>(key)).Should().BeNull();
    }

    [Fact]
    public async Task AtomicCounters_AreNotSupported_ReturnNull()
    {
        var sut = Create();

        (await sut.IncrementAsync("c")).Should().BeNull();
        (await sut.IncrementByFloatAsync("c", 1.5)).Should().BeNull();
    }

    [Fact]
    public async Task Set_WithNullValue_IsIgnored()
    {
        var sut = Create();

        await sut.SetAsync<Payload?>("k", null);

        sut.Count.Should().Be(0);
    }

    // ---------- Selección de proveedor (CA-048-04, Lambda sin ElastiCache) ----------

    [Theory]
    [InlineData("false", "localhost:6379", typeof(MemoryCacheService))]
    [InlineData("true", "", typeof(MemoryCacheService))]
    [InlineData("true", "localhost:6379", typeof(RedisCacheService))]
    public void AddInfrastructure_PicksTheCacheProviderFromConfiguration(string redisEnabled, string connection, Type expected)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Redis:Enabled"] = redisEnabled,
                ["Redis:ConnectionString"] = connection
            })
            .Build();
        var services = new ServiceCollection();

        services.AddInfrastructure(configuration);

        services.Last(d => d.ServiceType == typeof(ICacheService)).ImplementationType.Should().Be(expected);
    }

    // ---------- Health check (FR-048-12, NFR-048-04) ----------

    [Fact]
    public async Task HealthCheck_WhenCacheIsAvailable_IsHealthyAndReportsOnlyTheProvider()
    {
        var cache = Substitute.For<ICacheService>();
        cache.IsAvailable.Returns(true);
        cache.Provider.Returns("memory");

        var result = await new CacheHealthCheck(cache).CheckHealthAsync(new HealthCheckContext());

        result.Status.Should().Be(HealthStatus.Healthy);
        result.Data.Should().ContainKey("provider").WhoseValue.Should().Be("memory");
    }

    [Fact]
    public async Task HealthCheck_WhenCacheIsDown_IsDegradedNeverUnhealthy_SoReadinessStaysUp()
    {
        var cache = Substitute.For<ICacheService>();
        cache.IsAvailable.Returns(false);
        cache.Provider.Returns("redis");

        var result = await new CacheHealthCheck(cache).CheckHealthAsync(new HealthCheckContext());

        result.Status.Should().Be(HealthStatus.Degraded);
        result.Description.Should().NotContain("localhost").And.NotContain("6379");
    }
}
