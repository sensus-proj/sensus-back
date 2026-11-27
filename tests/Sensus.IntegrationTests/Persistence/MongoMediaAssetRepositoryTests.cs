using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using Sensus.Domain.Entities;
using Sensus.Infrastructure.Persistence.Mongo;
using Sensus.Infrastructure.Persistence.Mongo.Repositories;
using Testcontainers.MongoDb;

namespace Sensus.IntegrationTests.Persistence;

/// <summary>
/// Pruebas reales contra MongoDB (Testcontainers) para verificar el índice <c>ix_media_assets_owner_secure_url</c>
/// y <c>IsApprovedOwnedUrlAsync</c> (Spec 003 / A2).
/// </summary>
public sealed class MongoMediaAssetRepositoryTests : IAsyncLifetime
{
    private readonly string? _envConn = Environment.GetEnvironmentVariable("MONGO_CONNECTION_STRING");
    private readonly string _databaseName = $"sensus_media_assets_{Guid.NewGuid():N}";
    private MongoDbContainer? _container;
    private string _connectionString = string.Empty;
    private MongoDbContext _context = null!;
    private MongoMediaAssetRepository _repository = null!;

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
        _repository = new MongoMediaAssetRepository(_context, NullLogger<MongoMediaAssetRepository>.Instance);
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
    public async Task IsApprovedOwnedUrlAsync_VerifiesIndexAndMatchesOnlyApprovedOwnedUrl()
    {
        List<BsonDocument> indexes = await (await _context.MediaAssets.Indexes.ListAsync()).ToListAsync();
        indexes.Should().Contain(i => i["name"] == "ix_media_assets_owner_secure_url");

        Guid ownerId = Guid.NewGuid();
        Guid otherId = Guid.NewGuid();
        DateTime now = new(2026, 11, 26, 12, 0, 0, DateTimeKind.Utc);

        string approvedUrl = "https://res.cloudinary.com/nchjiueb/image/authenticated/v1/owned.jpg";
        string quarantinedUrl = "https://res.cloudinary.com/nchjiueb/image/authenticated/v1/quarantined.jpg";
        string foreignUrl = "https://res.cloudinary.com/nchjiueb/image/authenticated/v1/foreign.jpg";

        await _repository.AddAsync(MediaAsset.Create(ownerId, "p1", approvedUrl, "image/jpeg", MediaAssetStatus.Approved, now));
        await _repository.AddAsync(MediaAsset.Create(ownerId, "p2", quarantinedUrl, "image/jpeg", MediaAssetStatus.Quarantined, now));
        await _repository.AddAsync(MediaAsset.Create(otherId, "p3", foreignUrl, "image/jpeg", MediaAssetStatus.Approved, now));

        (await _repository.IsApprovedOwnedUrlAsync(ownerId, approvedUrl)).Should().BeTrue();
        (await _repository.IsApprovedOwnedUrlAsync(ownerId, quarantinedUrl)).Should().BeFalse();
        (await _repository.IsApprovedOwnedUrlAsync(ownerId, foreignUrl)).Should().BeFalse();
        (await _repository.IsApprovedOwnedUrlAsync(ownerId, "https://res.cloudinary.com/nchjiueb/image/authenticated/v1/missing.jpg")).Should().BeFalse();
    }

    [Fact]
    public async Task QuarantineLifecycle_GetByIdQueryExpiredAndUpdate_WorksAgainstRealMongoDb()
    {
        Guid ownerId = Guid.NewGuid();
        DateTime oldUtc = new(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
        DateTime cutoffUtc = new(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc);

        MediaAsset quarantined = MediaAsset.Create(ownerId, "q-old", null, "image/jpeg", MediaAssetStatus.Quarantined, oldUtc);
        await _repository.AddAsync(quarantined);

        MediaAsset? fetched = await _repository.GetByIdAsync(quarantined.Id);
        fetched.Should().NotBeNull();
        fetched!.Status.Should().Be(MediaAssetStatus.Quarantined);

        IReadOnlyList<MediaAsset> expired = await _repository.GetQuarantinedCreatedBeforeAsync(cutoffUtc, 10);
        expired.Should().Contain(a => a.Id == quarantined.Id);

        fetched.Release().Should().BeTrue();
        await _repository.UpdateAsync(fetched);

        MediaAsset? updated = await _repository.GetByIdAsync(quarantined.Id);
        updated!.Status.Should().Be(MediaAssetStatus.Approved);
    }
}
