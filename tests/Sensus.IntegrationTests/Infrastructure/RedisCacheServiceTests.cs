using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Sensus.Infrastructure.Cache;
using StackExchange.Redis;
using Xunit;

namespace Sensus.IntegrationTests.Infrastructure;

public sealed class RedisCacheServiceTests
{
    private readonly ILogger<RedisCacheService> _logger = Substitute.For<ILogger<RedisCacheService>>();

    private sealed record TestPayload(string Name, int Value);

    [Fact]
    public void RedisSettings_GetTtlForEntity_ReturnsConfiguredOrFallback()
    {
        var settings = new RedisSettings
        {
            DefaultTtlMinutes = 20,
            TtlByEntityMinutes = { ["UserProfile"] = 10 }
        };

        settings.GetTtlForEntity("UserProfile").Should().Be(TimeSpan.FromMinutes(10));
        settings.GetTtlForEntity("NonExistent").Should().Be(TimeSpan.FromMinutes(20));
    }

    [Fact]
    public async Task WhenRedisIsDisabled_OperationsCompleteGracefully()
    {
        var options = Options.Create(new RedisSettings { Enabled = false });
        var sut = new RedisCacheService(options, _logger, connection: null);

        var getResult = await sut.GetAsync<TestPayload>("test:key");
        getResult.Should().BeNull();

        var exists = await sut.ExistsAsync("test:key");
        exists.Should().BeFalse();

        var actSet = () => sut.SetAsync("test:key", new TestPayload("Sample", 42));
        await actSet.Should().NotThrowAsync();

        var actRemove = () => sut.RemoveAsync("test:key");
        await actRemove.Should().NotThrowAsync();

        var actRemovePrefix = () => sut.RemoveByPrefixAsync("test:");
        await actRemovePrefix.Should().NotThrowAsync();
    }

    [Fact]
    public async Task WhenRedisThrowsException_OperationsFailGracefullyWithoutCrashing()
    {
        var options = Options.Create(new RedisSettings { Enabled = true });
        var connection = Substitute.For<IConnectionMultiplexer>();
        var db = Substitute.For<IDatabase>();

        connection.IsConnected.Returns(true);
        connection.GetDatabase(Arg.Any<int>(), Arg.Any<object>()).Returns(db);

        db.StringGetAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>())
            .ThrowsAsync(new RedisException("Redis connection error"));
        db.StringSetAsync(Arg.Any<RedisKey>(), Arg.Any<RedisValue>(), Arg.Any<TimeSpan?>(), Arg.Any<bool>(), Arg.Any<When>(), Arg.Any<CommandFlags>())
            .ThrowsAsync(new RedisException("Redis timeout error"));
        db.KeyDeleteAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>())
            .ThrowsAsync(new RedisException("Redis internal failure"));
        db.KeyExistsAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>())
            .ThrowsAsync(new RedisException("Socket closed"));

        var sut = new RedisCacheService(options, _logger, connection);

        // GetAsync should log warning and return default(T)
        var getResult = await sut.GetAsync<TestPayload>("test:key");
        getResult.Should().BeNull();

        // ExistsAsync should log warning and return false
        var exists = await sut.ExistsAsync("test:key");
        exists.Should().BeFalse();

        // SetAsync and RemoveAsync should not throw
        var actSet = () => sut.SetAsync("test:key", new TestPayload("Sample", 99));
        await actSet.Should().NotThrowAsync();

        var actRemove = () => sut.RemoveAsync("test:key");
        await actRemove.Should().NotThrowAsync();
    }

    [Fact]
    public async Task WhenRedisIsHealthy_GetAndSetExecuteProperly()
    {
        var options = Options.Create(new RedisSettings { Enabled = true, DefaultTtlMinutes = 30 });
        var connection = Substitute.For<IConnectionMultiplexer>();
        var db = Substitute.For<IDatabase>();

        connection.IsConnected.Returns(true);
        connection.GetDatabase(Arg.Any<int>(), Arg.Any<object>()).Returns(db);

        const string json = "{\"Name\":\"SensusUser\",\"Value\":123}";
        db.StringGetAsync("user:123", Arg.Any<CommandFlags>()).Returns((RedisValue)json);
        db.KeyExistsAsync("user:123", Arg.Any<CommandFlags>()).Returns(true);

        var sut = new RedisCacheService(options, _logger, connection);

        var item = await sut.GetAsync<TestPayload>("user:123");
        item.Should().NotBeNull();
        item!.Name.Should().Be("SensusUser");
        item.Value.Should().Be(123);

        var exists = await sut.ExistsAsync("user:123");
        exists.Should().BeTrue();

        await sut.SetAsync("user:123", new TestPayload("Updated", 456), TimeSpan.FromMinutes(10));
        await db.Received(1).StringSetAsync(
            "user:123",
            Arg.Is<RedisValue>(v => v.ToString().Contains("Updated")),
            TimeSpan.FromMinutes(10));

        await sut.RemoveAsync("user:123");
        await db.Received(1).KeyDeleteAsync("user:123");
    }
}
