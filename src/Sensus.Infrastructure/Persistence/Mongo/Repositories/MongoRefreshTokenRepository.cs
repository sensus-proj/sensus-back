using MongoDB.Driver;
using Sensus.Domain.Repositories;
using Sensus.Infrastructure.Persistence.Mongo.Documents;
using RefreshTokenEntity = Sensus.Domain.Entities.RefreshToken;

namespace Sensus.Infrastructure.Persistence.Mongo.Repositories;

public sealed class MongoRefreshTokenRepository : IRefreshTokenRepository
{
    private readonly MongoDbContext _context;

    public MongoRefreshTokenRepository(MongoDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(RefreshTokenEntity token, CancellationToken cancellationToken = default)
    {
        await _context.RefreshTokens.InsertOneAsync(
            RefreshTokenDocument.FromDomain(token),
            cancellationToken: cancellationToken);
    }

    public async Task<RefreshTokenEntity?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default)
    {
        RefreshTokenDocument? doc = await _context.RefreshTokens
            .Find(t => t.TokenHash == tokenHash)
            .FirstOrDefaultAsync(cancellationToken);

        return doc?.ToDomain();
    }

    public async Task<bool> TryMarkAsUsedAsync(
        Guid tokenId,
        Guid replacedByTokenId,
        DateTime usedAtUtc,
        CancellationToken cancellationToken = default)
    {
        DateTime normalizedNow = DateTime.SpecifyKind(usedAtUtc, DateTimeKind.Utc);
        var builder = Builders<RefreshTokenDocument>.Filter;
        FilterDefinition<RefreshTokenDocument> filter = builder.And(
            builder.Eq(t => t.Id, tokenId),
            builder.Eq(t => t.UsedAtUtc, null),
            builder.Eq(t => t.RevokedAtUtc, null),
            builder.Gt(t => t.ExpiresAtUtc, normalizedNow));

        UpdateDefinition<RefreshTokenDocument> update = Builders<RefreshTokenDocument>.Update
            .Set(t => t.UsedAtUtc, normalizedNow)
            .Set(t => t.ReplacedByTokenId, replacedByTokenId);

        UpdateResult result = await _context.RefreshTokens.UpdateOneAsync(filter, update, cancellationToken: cancellationToken);
        return result.ModifiedCount == 1;
    }

    public async Task<int> RevokeFamilyAsync(
        Guid familyId,
        DateTime revokedAtUtc,
        string reason,
        CancellationToken cancellationToken = default)
    {
        DateTime normalizedNow = DateTime.SpecifyKind(revokedAtUtc, DateTimeKind.Utc);
        var builder = Builders<RefreshTokenDocument>.Filter;
        FilterDefinition<RefreshTokenDocument> filter = builder.And(
            builder.Eq(t => t.FamilyId, familyId),
            builder.Eq(t => t.RevokedAtUtc, null));

        UpdateDefinition<RefreshTokenDocument> update = Builders<RefreshTokenDocument>.Update
            .Set(t => t.RevokedAtUtc, normalizedNow)
            .Set(t => t.RevocationReason, reason);

        UpdateResult result = await _context.RefreshTokens.UpdateManyAsync(filter, update, cancellationToken: cancellationToken);
        return (int)Math.Min(result.ModifiedCount, int.MaxValue);
    }

    public async Task<int> RevokeAllByUserAsync(
        Guid userId,
        DateTime revokedAtUtc,
        string reason,
        CancellationToken cancellationToken = default)
    {
        DateTime normalizedNow = DateTime.SpecifyKind(revokedAtUtc, DateTimeKind.Utc);
        var builder = Builders<RefreshTokenDocument>.Filter;
        FilterDefinition<RefreshTokenDocument> filter = builder.And(
            builder.Eq(t => t.UserId, userId),
            builder.Eq(t => t.RevokedAtUtc, null));

        UpdateDefinition<RefreshTokenDocument> update = Builders<RefreshTokenDocument>.Update
            .Set(t => t.RevokedAtUtc, normalizedNow)
            .Set(t => t.RevocationReason, reason);

        UpdateResult result = await _context.RefreshTokens.UpdateManyAsync(filter, update, cancellationToken: cancellationToken);
        return (int)Math.Min(result.ModifiedCount, int.MaxValue);
    }
}
