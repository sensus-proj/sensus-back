using MongoDB.Driver;
using Sensus.Domain.Entities;
using Sensus.Domain.Repositories;
using Sensus.Infrastructure.Persistence.Mongo.Documents;

namespace Sensus.Infrastructure.Persistence.Mongo.Repositories;

/// <summary>
/// Implementación MongoDB de los me gusta de publicaciones (Spec 039).
/// La unicidad la garantiza el índice único <c>ux_feed_post_likes_post_user</c>: una inserción concurrente
/// duplicada se resuelve como "ya existía" y no altera el conteo (FR-039-04, FR-039-05, CA-039-04).
/// El conteo se deriva de los registros únicos con el mismo índice, por lo que nunca es negativo
/// ni se desincroniza (NFR-039-02).
/// </summary>
public sealed class MongoFeedPostLikeRepository : IFeedPostLikeRepository
{
    private readonly MongoDbContext _context;

    public MongoFeedPostLikeRepository(MongoDbContext context)
    {
        _context = context;
    }

    public async Task<bool> AddAsync(FeedPostLike like, CancellationToken cancellationToken = default)
    {
        try
        {
            await _context.FeedPostLikes.InsertOneAsync(
                FeedPostLikeDocument.FromDomain(like),
                cancellationToken: cancellationToken);
            return true;
        }
        catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            return false;
        }
    }

    public async Task<bool> RemoveAsync(Guid postId, Guid userId, CancellationToken cancellationToken = default)
    {
        FilterDefinition<FeedPostLikeDocument> filter = Builders<FeedPostLikeDocument>.Filter.And(
            Builders<FeedPostLikeDocument>.Filter.Eq(l => l.PostId, postId),
            Builders<FeedPostLikeDocument>.Filter.Eq(l => l.UserId, userId));

        DeleteResult result = await _context.FeedPostLikes.DeleteOneAsync(filter, cancellationToken);
        return result.DeletedCount > 0;
    }

    public async Task<int> CountByPostIdAsync(Guid postId, CancellationToken cancellationToken = default)
    {
        long count = await _context.FeedPostLikes.CountDocumentsAsync(
            Builders<FeedPostLikeDocument>.Filter.Eq(l => l.PostId, postId),
            cancellationToken: cancellationToken);

        return FeedPostLike.ClampCount(count);
    }
}
