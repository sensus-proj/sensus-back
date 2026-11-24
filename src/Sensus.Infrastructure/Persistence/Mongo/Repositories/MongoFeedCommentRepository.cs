using MongoDB.Driver;
using Sensus.Domain.Entities;
using Sensus.Domain.Repositories;
using Sensus.Infrastructure.Persistence.Mongo.Documents;

namespace Sensus.Infrastructure.Persistence.Mongo.Repositories;

/// <summary>
/// Implementación MongoDB del repositorio de comentarios del feed (Spec 025).
/// </summary>
public sealed class MongoFeedCommentRepository : IFeedCommentRepository
{
    private readonly MongoDbContext _context;

    public MongoFeedCommentRepository(MongoDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(FeedComment comment, CancellationToken cancellationToken = default)
    {
        await _context.FeedComments.InsertOneAsync(
            FeedCommentDocument.FromDomain(comment),
            cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyList<FeedComment>> GetByPostIdPagedAsync(
        Guid postId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        int skip = (Math.Max(1, pageNumber) - 1) * Math.Max(1, pageSize);

        List<FeedCommentDocument> docs = await _context.FeedComments
            .Find(c => c.PostId == postId)
            .SortBy(c => c.CreatedAtUtc)
            .Skip(skip)
            .Limit(Math.Max(1, pageSize))
            .ToListAsync(cancellationToken);

        return docs.Select(d => d.ToDomain()).ToList();
    }

    public async Task<int> CountByPostIdAsync(Guid postId, CancellationToken cancellationToken = default)
    {
        long count = await _context.FeedComments.CountDocumentsAsync(
            c => c.PostId == postId,
            cancellationToken: cancellationToken);

        return (int)count;
    }
}
