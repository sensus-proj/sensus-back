// Ref: FR-021-01 / FR-021-03 / NFR-021-01 / B5 - Prueba de persistencia real con Testcontainers
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using Sensus.Application.Common.Settings;
using Sensus.Domain.Entities;
using Sensus.Domain.Enums;
using Sensus.Infrastructure.Persistence.Mongo;
using Sensus.Infrastructure.Persistence.Mongo.Documents;
using Sensus.Infrastructure.Persistence.Mongo.Repositories;
using Testcontainers.MongoDb;

namespace Sensus.IntegrationTests.Persistence;

public sealed class MongoModerationAuditRepositoryTests : IAsyncLifetime
{
    private static readonly DateTime BaseTime = new(2026, 11, 22, 12, 0, 0, DateTimeKind.Utc);

    private readonly string? _envConn = Environment.GetEnvironmentVariable("MONGO_CONNECTION_STRING");
    private readonly string _databaseName = $"sensus_audit_{Guid.NewGuid():N}";
    private MongoDbContainer? _container;
    private string _connectionString = string.Empty;
    private MongoDbContext _context = null!;
    private MongoModerationAuditRepository _repository = null!;

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

        _repository = new MongoModerationAuditRepository(_context);
    }

    public async Task DisposeAsync()
    {
        if (!string.IsNullOrWhiteSpace(_connectionString))
        {
            await new MongoClient(_connectionString).DropDatabaseAsync(_databaseName);
        }

        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }

    [Fact]
    public async Task AddAsync_And_GetPagedAsync_ShouldPersistAndRehydrateRecordAccurately()
    {
        var targetId = Guid.NewGuid();
        var record = ModerationAuditRecord.Create(
            targetId: targetId,
            targetType: ModerationTargetType.MediaAsset,
            verdict: ModerationVerdict.Approved,
            decision: "Approved",
            contentContext: "media_upload",
            flaggedCategories: ["harassment"],
            categoryScores: new Dictionary<string, double> { ["harassment"] = 0.05 },
            thresholdApplied: "strict_v1",
            actorType: ModerationActorType.User,
            actorId: "user-123",
            correlationId: "corr-xyz",
            createdAtUtc: BaseTime,
            retentionDays: 2555).Value;

        await _repository.AddAsync(record, CancellationToken.None);

        var paged = await _repository.ListAsync(
            targetId: targetId,
            targetType: null,
            verdict: null,
            fromUtc: null,
            toUtc: null,
            page: 1,
            pageSize: 10,
            CancellationToken.None);

        paged.TotalCount.Should().Be(1);
        paged.Items.Should().HaveCount(1);

        var loaded = paged.Items[0];
        loaded.TargetId.Should().Be(targetId);
        loaded.TargetType.Should().Be(ModerationTargetType.MediaAsset);
        loaded.Verdict.Should().Be(ModerationVerdict.Approved);
        loaded.Decision.Should().Be("Approved");
        loaded.ContentContext.Should().Be("media_upload");
        loaded.FlaggedCategories.Should().ContainSingle().Which.Should().Be("harassment");
        loaded.CategoryScores.Should().ContainKey("harassment").WhoseValue.Should().BeApproximately(0.05, 0.001);
        loaded.ThresholdApplied.Should().Be("strict_v1");
        loaded.ActorType.Should().Be(ModerationActorType.User);
        loaded.ActorId.Should().Be("user-123");
        loaded.CorrelationId.Should().Be("corr-xyz");
        loaded.CreatedAtUtc.Should().Be(BaseTime);
        loaded.RetainUntilUtc.Should().Be(BaseTime.AddDays(2555));
    }

    [Fact]
    public async Task ListAsync_ShouldPaginateAndSortDescendingByCreatedAtUtc()
    {
        for (int i = 1; i <= 5; i++)
        {
            var record = ModerationAuditRecord.Create(
                targetId: Guid.NewGuid(),
                targetType: ModerationTargetType.FeedComment,
                verdict: ModerationVerdict.Approved,
                decision: "Approved",
                contentContext: "comment",
                flaggedCategories: [],
                categoryScores: null,
                thresholdApplied: null,
                actorType: ModerationActorType.User,
                actorId: $"user-{i}",
                correlationId: $"corr-{i}",
                createdAtUtc: BaseTime.AddMinutes(i),
                retentionDays: 2555).Value;

            await _repository.AddAsync(record, CancellationToken.None);
        }

        // Page 1: items with createdAtUtc +5 and +4 minutes (descending)
        var page1 = await _repository.ListAsync(
            targetId: null, targetType: null, verdict: null, fromUtc: null, toUtc: null,
            page: 1, pageSize: 2, CancellationToken.None);

        page1.TotalCount.Should().Be(5);
        page1.Items.Should().HaveCount(2);
        page1.Items[0].ActorId.Should().Be("user-5");
        page1.Items[1].ActorId.Should().Be("user-4");

        // Page 2: items with createdAtUtc +3 and +2 minutes
        var page2 = await _repository.ListAsync(
            targetId: null, targetType: null, verdict: null, fromUtc: null, toUtc: null,
            page: 2, pageSize: 2, CancellationToken.None);

        page2.TotalCount.Should().Be(5);
        page2.Items.Should().HaveCount(2);
        page2.Items[0].ActorId.Should().Be("user-3");
        page2.Items[1].ActorId.Should().Be("user-2");
    }

    [Fact]
    public async Task GetPagedAsync_WithFilters_ShouldReturnOnlyMatchingRecords()
    {
        var targetA = Guid.NewGuid();
        var targetB = Guid.NewGuid();

        var record1 = ModerationAuditRecord.Create(
            targetId: targetA,
            targetType: ModerationTargetType.Offer,
            verdict: ModerationVerdict.Approved,
            decision: "Approved",
            contentContext: "offer",
            flaggedCategories: [],
            categoryScores: null,
            thresholdApplied: null,
            actorType: ModerationActorType.User,
            actorId: "actor-1",
            correlationId: "corr-1",
            createdAtUtc: BaseTime,
            retentionDays: 2555).Value;

        var record2 = ModerationAuditRecord.Create(
            targetId: targetB,
            targetType: ModerationTargetType.Offer,
            verdict: ModerationVerdict.Rejected,
            decision: "Rejected",
            contentContext: "offer",
            flaggedCategories: ["hate"],
            categoryScores: null,
            thresholdApplied: null,
            actorType: ModerationActorType.System,
            actorId: "system",
            correlationId: "corr-2",
            createdAtUtc: BaseTime.AddHours(2),
            retentionDays: 2555).Value;

        var record3 = ModerationAuditRecord.Create(
            targetId: targetA,
            targetType: ModerationTargetType.FeedComment,
            verdict: ModerationVerdict.Quarantined,
            decision: "Quarantined",
            contentContext: "comment",
            flaggedCategories: ["spam"],
            categoryScores: null,
            thresholdApplied: null,
            actorType: ModerationActorType.Administrator,
            actorId: "admin",
            correlationId: "corr-3",
            createdAtUtc: BaseTime.AddHours(4),
            retentionDays: 2555).Value;

        await _repository.AddAsync(record1, CancellationToken.None);
        await _repository.AddAsync(record2, CancellationToken.None);
        await _repository.AddAsync(record3, CancellationToken.None);

        // Filter by targetId
        var byTargetA = await _repository.ListAsync(targetA, null, null, null, null, 1, 10, CancellationToken.None);
        byTargetA.TotalCount.Should().Be(2);

        // Filter by targetType
        var byTypeOffer = await _repository.ListAsync(null, ModerationTargetType.Offer, null, null, null, 1, 10, CancellationToken.None);
        byTypeOffer.TotalCount.Should().Be(2);

        // Filter by verdict
        var byVerdictRejected = await _repository.ListAsync(null, null, ModerationVerdict.Rejected, null, null, 1, 10, CancellationToken.None);
        byVerdictRejected.TotalCount.Should().Be(1);
        byVerdictRejected.Items[0].TargetId.Should().Be(targetB);

        // Filter by date range (from +1h to +3h)
        var byDateRange = await _repository.ListAsync(
            null, null, null,
            fromUtc: BaseTime.AddHours(1),
            toUtc: BaseTime.AddHours(3),
            page: 1, pageSize: 10, CancellationToken.None);

        byDateRange.TotalCount.Should().Be(1);
        byDateRange.Items[0].TargetId.Should().Be(targetB);
    }

    [Fact]
    public async Task ModerationAuditCollection_ShouldHaveRequiredIndexesAndTtl()
    {
        var collection = _context.ModerationAudit;
        var cursor = await collection.Indexes.ListAsync();
        var indexDocs = await cursor.ToListAsync();

        var indexNames = indexDocs.Select(doc => doc["name"].AsString).ToList();

        indexNames.Should().Contain("ix_moderation_audit_created_at",
            "debe existir el índice ix_moderation_audit_created_at para orden cronológico eficiente");
        indexNames.Should().Contain("ix_moderation_audit_target",
            "debe existir el índice compuesto ix_moderation_audit_target");
        indexNames.Should().Contain("ttl_moderation_audit_retention",
            "debe existir el índice ttl_moderation_audit_retention");

        // Verify TTL configuration
        var ttlIndex = indexDocs.First(doc => doc["name"].AsString == "ttl_moderation_audit_retention");
        ttlIndex.Contains("expireAfterSeconds").Should().BeTrue();
        ttlIndex["expireAfterSeconds"].AsInt32.Should().Be(0);
        ttlIndex["key"]["retainUntilUtc"].AsInt32.Should().Be(1);

        // Verify CreatedAt index direction
        var createdAtIndex = indexDocs.First(doc => doc["name"].AsString == "ix_moderation_audit_created_at");
        createdAtIndex["key"]["createdAtUtc"].AsInt32.Should().Be(-1);

        // Verify Target compound index
        var targetIndex = indexDocs.First(doc => doc["name"].AsString == "ix_moderation_audit_target");
        targetIndex["key"]["targetType"].AsInt32.Should().Be(1);
        targetIndex["key"]["targetId"].AsInt32.Should().Be(1);
        targetIndex["key"]["createdAtUtc"].AsInt32.Should().Be(-1);
    }
}
