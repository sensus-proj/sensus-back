using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using Sensus.Infrastructure.Persistence.Mongo;
using Sensus.Infrastructure.Persistence.Mongo.Repositories;
using Testcontainers.MongoDb;
using RefreshTokenEntity = Sensus.Domain.Entities.RefreshToken;

namespace Sensus.IntegrationTests.Persistence;

public sealed class MongoRefreshTokenRepositoryTests : IAsyncLifetime
{
    private static readonly DateTime Now = new(2026, 11, 26, 12, 0, 0, DateTimeKind.Utc);
    private readonly string? _envConn = Environment.GetEnvironmentVariable("MONGO_CONNECTION_STRING");
    private readonly string _databaseName = $"sensus_refresh_{Guid.NewGuid():N}";
    private MongoDbContainer? _container;
    private string _connectionString = string.Empty;
    private MongoDbContext _context = null!;
    private MongoRefreshTokenRepository _repository = null!;

    public async Task InitializeAsync()
    {
        if (!string.IsNullOrWhiteSpace(_envConn))
        {
            _connectionString = _envConn;
        }
        else
        {
            _container = new MongoDbBuilder("mongo:7.0").Build();
            await _container.StartAsync();
            _connectionString = _container.GetConnectionString();
        }

        _context = new MongoDbContext(
            Options.Create(new MongoDbSettings { ConnectionString = _connectionString, DatabaseName = _databaseName }),
            NullLogger<MongoDbContext>.Instance);
        _repository = new MongoRefreshTokenRepository(_context);
    }

    public async Task DisposeAsync()
    {
        if (!string.IsNullOrWhiteSpace(_envConn))
        {
            await new MongoClient(_connectionString).DropDatabaseAsync(_databaseName);
        }

        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }

    [Fact]
    public async Task Indexes_TtlAndUniqueHashAreDeclared_AndTryMarkAsUsedIsAtomic()
    {
        List<BsonDocument> indexes = await (await _context.RefreshTokens.Indexes.ListAsync()).ToListAsync();
        indexes.Should().Contain(i => i["name"] == "ux_refresh_tokens_token_hash" && i["unique"] == true);
        indexes.Should().Contain(i => i["name"] == "ttl_refresh_tokens_expires_at");

        Guid userId = Guid.NewGuid();
        Guid familyId = Guid.NewGuid();
        var token = RefreshTokenEntity.Create(userId, "sha256-hash-unique", Now, Now.AddDays(30), familyId).Value;
        await _repository.AddAsync(token);

        Guid replacement1 = Guid.NewGuid();
        Guid replacement2 = Guid.NewGuid();
        bool[] attempts = await Task.WhenAll(
            _repository.TryMarkAsUsedAsync(token.Id, replacement1, Now),
            _repository.TryMarkAsUsedAsync(token.Id, replacement2, Now));

        attempts.Count(ok => ok).Should().Be(1);

        var secondInFamily = RefreshTokenEntity.Create(userId, "sha256-hash-second", Now, Now.AddDays(30), familyId, id: replacement1).Value;
        await _repository.AddAsync(secondInFamily);

        int revoked = await _repository.RevokeFamilyAsync(familyId, Now.AddMinutes(1), "reuse_detected");
        revoked.Should().BeGreaterThanOrEqualTo(1);

        RefreshTokenEntity? loaded = await _repository.GetByTokenHashAsync("sha256-hash-second");
        loaded.Should().NotBeNull();
        loaded!.RevokedAtUtc.Should().NotBeNull();
        loaded.RevocationReason.Should().Be("reuse_detected");
    }
}
