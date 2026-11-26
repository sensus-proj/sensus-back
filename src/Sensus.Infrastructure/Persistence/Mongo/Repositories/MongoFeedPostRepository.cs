using MongoDB.Bson;
using MongoDB.Driver;
using Sensus.Domain.Entities;
using Sensus.Domain.Enums;
using Sensus.Domain.Repositories;
using Sensus.Infrastructure.Persistence.Mongo.Documents;

namespace Sensus.Infrastructure.Persistence.Mongo.Repositories;

/// <summary>
/// Implementación MongoDB del repositorio de publicaciones del feed (Spec 024).
/// </summary>
public sealed class MongoFeedPostRepository : IFeedPostRepository
{
    internal const string NearbyIndexName = "ix_feed_posts_status_location_created_at";

    private readonly MongoDbContext _context;

    public MongoFeedPostRepository(MongoDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(FeedPost post, CancellationToken cancellationToken = default)
    {
        await _context.FeedPosts.InsertOneAsync(
            FeedPostDocument.FromDomain(post),
            cancellationToken: cancellationToken);
    }

    public async Task<FeedPost?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        FeedPostDocument? doc = await _context.FeedPosts
            .Find(p => p.Id == id)
            .FirstOrDefaultAsync(cancellationToken);

        return doc?.ToDomain();
    }

    public async Task UpdateAsync(FeedPost post, CancellationToken cancellationToken = default)
    {
        FeedPostDocument doc = FeedPostDocument.FromDomain(post);
        await _context.FeedPosts.ReplaceOneAsync(
            p => p.Id == post.Id,
            doc,
            new ReplaceOptions { IsUpsert = false },
            cancellationToken);
    }

    public async Task<IReadOnlyList<FeedPost>> GetPublishedPagedAsync(
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        int skip = (Math.Max(1, pageNumber) - 1) * Math.Max(1, pageSize);
        string publishedStatus = FeedPostStatus.Published.ToString();

        List<FeedPostDocument> docs = await _context.FeedPosts
            .Find(p => !p.IsDeleted && p.Status == publishedStatus)
            .SortByDescending(p => p.CreatedAtUtc)
            .Skip(skip)
            .Limit(Math.Max(1, pageSize))
            .ToListAsync(cancellationToken);

        return docs.Select(d => d.ToDomain()).ToList();
    }

    public async Task<int> CountPublishedAsync(CancellationToken cancellationToken = default)
    {
        string publishedStatus = FeedPostStatus.Published.ToString();

        long count = await _context.FeedPosts.CountDocumentsAsync(
            p => !p.IsDeleted && p.Status == publishedStatus,
            cancellationToken: cancellationToken);

        return (int)count;
    }

    public async Task<(IReadOnlyList<FeedPost> Items, int TotalCount)> GetPublishedNearbyPagedAsync(
        double latitude,
        double longitude,
        double radiusMeters,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        int safePage = Math.Max(1, pageNumber);
        int safeSize = Math.Max(1, pageSize);
        const double EarthRadiusMeters = 6_371_000d;
        double radiusRadians = Math.Max(1d, radiusMeters) / EarthRadiusMeters;

        // Lista de permitidos: solo estado publicado explícito y no eliminado (FR-026-02).
        // $geoWithin + $centerSphere usa el índice ix_feed_posts_status_location_created_at y excluye
        // documentos sin campo location (FR-026-05).
        var filter = new BsonDocument
        {
            { "status", FeedPostStatus.Published.ToString() },
            { "isDeleted", false },
            {
                "location", new BsonDocument("$geoWithin", new BsonDocument(
                    "$centerSphere",
                    new BsonArray { new BsonArray { longitude, latitude }, radiusRadians }))
            }
        };

        // CA-026-07: fecha descendente y _id como desempate estable entre páginas.
        var sort = new BsonDocument { { "createdAtUtc", -1 }, { "_id", -1 } };

        // NFR-026-02: se fija el índice declarado para no depender del planificador.
        Task<List<FeedPostDocument>> pageTask = _context.FeedPosts
            .Find(filter, new FindOptions { Hint = NearbyIndexName })
            .Sort(sort)
            .Skip((safePage - 1) * safeSize)
            .Limit(safeSize)
            .ToListAsync(cancellationToken);

        Task<long> countTask = _context.FeedPosts.CountDocumentsAsync(
            filter,
            new CountOptions { Hint = NearbyIndexName },
            cancellationToken);

        await Task.WhenAll(pageTask, countTask);

        IReadOnlyList<FeedPost> items = (await pageTask).Select(d => d.ToDomain()).ToList();
        int total = (int)Math.Min(await countTask, int.MaxValue);
        return (items, total);
    }

    public async Task<int> AnonymizeByAuthorAsync(
        Guid authorUserId,
        DateTime updatedAtUtc,
        CancellationToken cancellationToken = default)
    {
        FilterDefinition<FeedPostDocument> filter = Builders<FeedPostDocument>.Filter.And(
            Builders<FeedPostDocument>.Filter.Eq(p => p.AuthorUserId, authorUserId),
            Builders<FeedPostDocument>.Filter.Ne(p => p.AuthorUserId, Guid.Empty));

        UpdateDefinition<FeedPostDocument> update = Builders<FeedPostDocument>.Update
            .Set(p => p.AuthorUserId, Guid.Empty)
            .Set(p => p.AuthorDisplayName, FeedPost.DefaultAuthorDisplayName)
            .Set(p => p.AuthorPhotoUrl, null)
            .Set(p => p.UpdatedAtUtc, DateTime.SpecifyKind(updatedAtUtc, DateTimeKind.Utc));

        var result = await _context.FeedPosts.UpdateManyAsync(filter, update, cancellationToken: cancellationToken);
        return (int)Math.Min(result.ModifiedCount, int.MaxValue);
    }

    public async Task<int> PurgeQuarantinedBeforeAsync(
        DateTime cutoffUtc,
        CancellationToken cancellationToken = default)
    {
        DateTime normalizedCutoff = DateTime.SpecifyKind(cutoffUtc, DateTimeKind.Utc);
        string quarantinedStatus = FeedPostStatus.Quarantined.ToString();

        DeleteResult result = await _context.FeedPosts.DeleteManyAsync(
            p => p.Status == quarantinedStatus && !p.IsDeleted && p.CreatedAtUtc < normalizedCutoff,
            cancellationToken);

        return (int)Math.Min(result.DeletedCount, int.MaxValue);
    }
}
