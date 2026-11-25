using MongoDB.Driver;
using Sensus.Domain.Entities;
using Sensus.Domain.Enums;
using Sensus.Domain.Repositories;
using Sensus.Infrastructure.Persistence.Mongo.Documents;

namespace Sensus.Infrastructure.Persistence.Mongo.Repositories;

public sealed class MongoModerationAuditRepository : IModerationAuditRepository
{
    private readonly MongoDbContext _context;

    public MongoModerationAuditRepository(MongoDbContext context)
    {
        _context = context;
    }

    public async Task<bool> AddIfNotExistsAsync(
        ModerationAudit entry,
        CancellationToken cancellationToken = default)
    {
        FilterDefinition<ModerationAuditDocument> filter =
            Builders<ModerationAuditDocument>.Filter.Eq(a => a.Id, entry.Id);

        var result = await _context.ModerationAudit.ReplaceOneAsync(
            filter,
            ModerationAuditDocument.FromDomain(entry),
            new ReplaceOptions { IsUpsert = true },
            cancellationToken);

        return result.UpsertedId is not null;
    }

    public Task AddAsync(ModerationAuditRecord record, CancellationToken cancellationToken = default) =>
        _context.ModerationAudit.InsertOneAsync(
            ModerationAuditDocument.FromDomain(record),
            cancellationToken: cancellationToken);

    public async Task<(IReadOnlyList<ModerationAuditRecord> Items, long TotalCount)> ListAsync(
        Guid? targetId,
        ModerationTargetType? targetType,
        ModerationVerdict? verdict,
        DateTime? fromUtc,
        DateTime? toUtc,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        FilterDefinitionBuilder<ModerationAuditDocument> filterBuilder = Builders<ModerationAuditDocument>.Filter;
        FilterDefinition<ModerationAuditDocument> filter = filterBuilder.Eq(record => record.SubjectKey, string.Empty);

        if (targetId is { } id)
        {
            filter &= filterBuilder.Eq(record => record.TargetId, id);
        }

        if (targetType is { } type)
        {
            filter &= filterBuilder.Eq(record => record.TargetType, type);
        }

        if (verdict is { } value)
        {
            filter &= filterBuilder.Eq(record => record.Verdict, value);
        }

        if (fromUtc is { } from)
        {
            filter &= filterBuilder.Gte(record => record.CreatedAtUtc, from);
        }

        if (toUtc is { } to)
        {
            filter &= filterBuilder.Lte(record => record.CreatedAtUtc, to);
        }

        long total = await _context.ModerationAudit.CountDocumentsAsync(filter, cancellationToken: cancellationToken);

        List<ModerationAuditDocument> documents = await _context.ModerationAudit
            .Find(filter)
            .SortByDescending(record => record.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Limit(pageSize)
            .ToListAsync(cancellationToken);

        return (documents.Select(document => document.ToDomain()).ToList(), total);
    }
}
