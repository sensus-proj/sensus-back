using MongoDB.Driver;
using Sensus.Domain.Entities;
using Sensus.Domain.Repositories;
using Sensus.Infrastructure.Persistence.Mongo.Documents;

namespace Sensus.Infrastructure.Persistence.Mongo.Repositories;

/// <summary>
/// Implementación en MongoDB Atlas para solicitudes de amistad y vínculos vecinales.
/// </summary>
public sealed class MongoFriendRequestRepository : IFriendRequestRepository
{
    private readonly MongoDbContext _context;

    public MongoFriendRequestRepository(MongoDbContext context)
    {
        _context = context;
    }

    public async Task<FriendRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var filter = Builders<FriendRequestDocument>.Filter.Eq(f => f.Id, id);
        var document = await _context.FriendRequests.Find(filter).FirstOrDefaultAsync(cancellationToken);
        return document?.ToDomain();
    }

    public async Task<FriendRequest?> GetBetweenUsersAsync(
        Guid userA,
        Guid userB,
        CancellationToken cancellationToken = default)
    {
        var direct = Builders<FriendRequestDocument>.Filter.And(
            Builders<FriendRequestDocument>.Filter.Eq(f => f.SenderUserId, userA),
            Builders<FriendRequestDocument>.Filter.Eq(f => f.TargetUserId, userB));

        var reverse = Builders<FriendRequestDocument>.Filter.And(
            Builders<FriendRequestDocument>.Filter.Eq(f => f.SenderUserId, userB),
            Builders<FriendRequestDocument>.Filter.Eq(f => f.TargetUserId, userA));

        var filter = Builders<FriendRequestDocument>.Filter.Or(direct, reverse);
        var document = await _context.FriendRequests
            .Find(filter)
            .SortByDescending(f => f.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        return document?.ToDomain();
    }

    public async Task<IReadOnlyList<FriendRequest>> GetForUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var filter = Builders<FriendRequestDocument>.Filter.Or(
            Builders<FriendRequestDocument>.Filter.Eq(f => f.SenderUserId, userId),
            Builders<FriendRequestDocument>.Filter.Eq(f => f.TargetUserId, userId));

        var documents = await _context.FriendRequests
            .Find(filter)
            .SortByDescending(f => f.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        return documents.Select(d => d.ToDomain()).ToList();
    }

    public async Task AddAsync(FriendRequest request, CancellationToken cancellationToken = default)
    {
        var document = FriendRequestDocument.FromDomain(request);
        await _context.FriendRequests.InsertOneAsync(document, cancellationToken: cancellationToken);
    }

    public async Task UpdateAsync(FriendRequest request, CancellationToken cancellationToken = default)
    {
        var document = FriendRequestDocument.FromDomain(request);
        var filter = Builders<FriendRequestDocument>.Filter.Eq(f => f.Id, request.Id);
        await _context.FriendRequests.ReplaceOneAsync(filter, document, cancellationToken: cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var filter = Builders<FriendRequestDocument>.Filter.Eq(f => f.Id, id);
        await _context.FriendRequests.DeleteOneAsync(filter, cancellationToken);
    }
}
