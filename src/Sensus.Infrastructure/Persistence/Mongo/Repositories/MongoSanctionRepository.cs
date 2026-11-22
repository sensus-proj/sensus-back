using MongoDB.Driver;
using Sensus.Domain.Entities;
using Sensus.Domain.Enums;
using Sensus.Domain.Repositories;
using Sensus.Infrastructure.Persistence.Mongo.Documents;

namespace Sensus.Infrastructure.Persistence.Mongo.Repositories;

/// <summary>
/// Persistencia de sanciones en MongoDB (Spec 020). Sin operación de borrado: registro de solo adición.
/// </summary>
public sealed class MongoSanctionRepository : ISanctionRepository
{
    private readonly MongoDbContext _context;

    public MongoSanctionRepository(MongoDbContext context)
    {
        _context = context;
    }

    public async Task<Sanction?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        SanctionDocument? document = await _context.Sanctions
            .Find(Builders<SanctionDocument>.Filter.Eq(s => s.Id, id))
            .FirstOrDefaultAsync(cancellationToken);
        return document?.ToDomain();
    }

    public async Task<Sanction?> GetByMediaIdAsync(Guid mediaId, CancellationToken cancellationToken = default)
    {
        SanctionDocument? document = await _context.Sanctions
            .Find(Builders<SanctionDocument>.Filter.Eq(s => s.Evidence.MediaId, mediaId))
            .FirstOrDefaultAsync(cancellationToken);
        return document?.ToDomain();
    }

    public async Task<IReadOnlyList<Sanction>> GetActiveByUserAsync(
        Guid userId,
        DateTime nowUtc,
        CancellationToken cancellationToken = default)
    {
        List<SanctionDocument> documents = await _context.Sanctions
            .Find(ActiveFilter(nowUtc) & Builders<SanctionDocument>.Filter.Eq(s => s.UserId, userId))
            .ToListAsync(cancellationToken);
        return documents.Select(d => d.ToDomain()).ToList();
    }

    public async Task<int> CountNotRevertedSinceAsync(
        Guid userId,
        DateTime sinceUtc,
        CancellationToken cancellationToken = default)
    {
        FilterDefinitionBuilder<SanctionDocument> f = Builders<SanctionDocument>.Filter;
        long count = await _context.Sanctions.CountDocumentsAsync(
            f.Eq(s => s.UserId, userId) &
            f.Gte(s => s.StartsAtUtc, sinceUtc) &
            f.Eq(s => s.RevertedAtUtc, null),
            cancellationToken: cancellationToken);
        return (int)Math.Min(count, int.MaxValue);
    }

    public async Task<(IReadOnlyList<Sanction> Items, long TotalCount)> ListAsync(
        Guid? userId,
        SanctionState? state,
        DateTime nowUtc,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        FilterDefinitionBuilder<SanctionDocument> f = Builders<SanctionDocument>.Filter;
        FilterDefinition<SanctionDocument> filter = f.Empty;

        if (userId is { } id)
        {
            filter &= f.Eq(s => s.UserId, id);
        }

        filter &= state switch
        {
            SanctionState.Active => ActiveFilter(nowUtc),
            SanctionState.Reverted => f.Ne(s => s.RevertedAtUtc, null),
            SanctionState.Expired => f.Eq(s => s.RevertedAtUtc, null) &
                                     f.Ne(s => s.EndsAtUtc, null) &
                                     f.Lte(s => s.EndsAtUtc, nowUtc),
            _ => f.Empty
        };

        long total = await _context.Sanctions.CountDocumentsAsync(filter, cancellationToken: cancellationToken);
        List<SanctionDocument> documents = await _context.Sanctions
            .Find(filter)
            .SortByDescending(s => s.StartsAtUtc)
            .Skip((page - 1) * pageSize)
            .Limit(pageSize)
            .ToListAsync(cancellationToken);

        return (documents.Select(d => d.ToDomain()).ToList(), total);
    }

    public Task AddAsync(Sanction sanction, CancellationToken cancellationToken = default) =>
        _context.Sanctions.InsertOneAsync(SanctionDocument.FromDomain(sanction), cancellationToken: cancellationToken);

    public Task UpdateAsync(Sanction sanction, CancellationToken cancellationToken = default) =>
        _context.Sanctions.ReplaceOneAsync(
            Builders<SanctionDocument>.Filter.Eq(s => s.Id, sanction.Id),
            SanctionDocument.FromDomain(sanction),
            cancellationToken: cancellationToken);

    private static FilterDefinition<SanctionDocument> ActiveFilter(DateTime nowUtc)
    {
        FilterDefinitionBuilder<SanctionDocument> f = Builders<SanctionDocument>.Filter;
        return f.Eq(s => s.RevertedAtUtc, null) &
               (f.Eq(s => s.EndsAtUtc, null) | f.Gt(s => s.EndsAtUtc, nowUtc));
    }
}
