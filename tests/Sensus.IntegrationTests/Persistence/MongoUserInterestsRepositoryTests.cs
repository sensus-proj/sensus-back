using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using Sensus.Domain.Entities;
using Sensus.Domain.Enums;
using Sensus.Infrastructure.Persistence.Mongo;
using Sensus.Infrastructure.Persistence.Mongo.Documents;
using Sensus.Infrastructure.Persistence.Mongo.Repositories;

namespace Sensus.IntegrationTests.Persistence;

/// <summary>
/// Pruebas de persistencia y mapeo de intereses del usuario (Spec 054, FR-054-02, FR-054-06).
/// El round-trip corre siempre; las pruebas contra MongoDB real corren cuando <c>MONGO_CONNECTION_STRING</c> está definida (CI).
/// </summary>
public sealed class MongoUserInterestsRepositoryTests : IAsyncLifetime
{
    private static readonly DateTime Now = new(2026, 11, 20, 12, 0, 0, DateTimeKind.Utc);

    private readonly string? _connectionString = Environment.GetEnvironmentVariable("MONGO_CONNECTION_STRING");
    private readonly string _databaseName = $"sensus_interests_{Guid.NewGuid():N}";
    private MongoDbContext? _context;
    private MongoUserInterestsRepository? _repository;

    public Task InitializeAsync()
    {
        if (!string.IsNullOrWhiteSpace(_connectionString))
        {
            _context = new MongoDbContext(
                Options.Create(new MongoDbSettings { ConnectionString = _connectionString, DatabaseName = _databaseName }),
                NullLogger<MongoDbContext>.Instance);
            _repository = new MongoUserInterestsRepository(_context);
        }

        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        if (!string.IsNullOrWhiteSpace(_connectionString))
        {
            await new MongoClient(_connectionString).DropDatabaseAsync(_databaseName);
        }
    }

    private bool Enabled => _context is not null && _repository is not null;

    [Fact]
    public void UserInterestsDocument_RoundTripsAllFields()
    {
        Guid userId = Guid.NewGuid();
        UserInterests entity = UserInterests.Create(userId, [OfferCategory.Bakery, OfferCategory.Dairy], Now).Value;

        UserInterests restored = UserInterestsDocument.FromDomain(entity).ToDomain();

        restored.Id.Should().Be(entity.Id);
        restored.UserId.Should().Be(userId);
        restored.Categories.Should().Equal(OfferCategory.Bakery, OfferCategory.Dairy);
        restored.UpdatedAtUtc.Should().Be(Now);
    }

    [Fact]
    public async Task UpsertGetAndDelete_InRealMongo_ShouldBeIdempotentAndHaveUniqueIndex()
    {
        if (!Enabled)
        {
            return;
        }

        List<BsonDocument> indexes = await (await _context!.UserInterests.Indexes.ListAsync()).ToListAsync();
        indexes.Should().Contain(idx => idx["name"] == "ux_user_interests_user_id" && idx["unique"] == true);

        Guid userId = Guid.NewGuid();
        UserInterests first = UserInterests.Create(userId, [OfferCategory.Bakery], Now).Value;
        await _repository!.UpsertAsync(first);

        UserInterests second = UserInterests.Create(userId, [OfferCategory.Dairy, OfferCategory.Pets], Now.AddMinutes(5)).Value;
        await _repository.UpsertAsync(second);

        UserInterests? loaded = await _repository.GetByUserIdAsync(userId);
        loaded.Should().NotBeNull();
        loaded!.Categories.Should().Equal(OfferCategory.Dairy, OfferCategory.Pets);

        await _repository.DeleteByUserIdAsync(userId);
        (await _repository.GetByUserIdAsync(userId)).Should().BeNull();
    }
}
