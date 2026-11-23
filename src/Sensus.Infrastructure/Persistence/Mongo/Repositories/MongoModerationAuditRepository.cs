using MongoDB.Driver;
using Sensus.Domain.Entities;
using Sensus.Domain.Repositories;
using Sensus.Infrastructure.Persistence.Mongo.Documents;

namespace Sensus.Infrastructure.Persistence.Mongo.Repositories;

/// <summary>
/// Persistencia de auditoría anonimizada de moderación (Spec 061, FR-061-05). El identificador es
/// determinista, por lo que un upsert mantiene la idempotencia sin duplicar entradas.
/// </summary>
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
}
