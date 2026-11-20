using MongoDB.Driver;
using Sensus.Domain.Entities;
using Sensus.Domain.Repositories;
using Sensus.Infrastructure.Persistence.Mongo.Documents;

namespace Sensus.Infrastructure.Persistence.Mongo.Repositories;

/// <summary>
/// Repositorio MongoDB de intereses del usuario sobre la colección <c>user_interests</c> (Spec 054).
/// </summary>
public sealed class MongoUserInterestsRepository : IUserInterestsRepository
{
    private readonly MongoDbContext _context;

    public MongoUserInterestsRepository(MongoDbContext context)
    {
        _context = context;
    }

    public async Task<UserInterests?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var filter = Builders<UserInterestsDocument>.Filter.Eq(d => d.UserId, userId);
        UserInterestsDocument? document = await _context.UserInterests
            .Find(filter)
            .FirstOrDefaultAsync(cancellationToken);

        return document?.ToDomain();
    }

    public async Task UpsertAsync(UserInterests interests, CancellationToken cancellationToken = default)
    {
        UserInterestsDocument doc = UserInterestsDocument.FromDomain(interests);
        var filter = Builders<UserInterestsDocument>.Filter.Eq(d => d.UserId, interests.UserId);
        var update = Builders<UserInterestsDocument>.Update
            .SetOnInsert(d => d.Id, doc.Id)
            .SetOnInsert(d => d.UserId, doc.UserId)
            .Set(d => d.Categories, doc.Categories)
            .Set(d => d.UpdatedAtUtc, doc.UpdatedAtUtc);

        await _context.UserInterests.UpdateOneAsync(
            filter,
            update,
            new UpdateOptions { IsUpsert = true },
            cancellationToken);
    }

    public async Task DeleteByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var filter = Builders<UserInterestsDocument>.Filter.Eq(d => d.UserId, userId);
        await _context.UserInterests.DeleteManyAsync(filter, cancellationToken);
    }
}
