using MongoDB.Bson;
using MongoDB.Driver;
using Sensus.Application.Common.Interfaces;
using Sensus.Domain.Enums;

namespace Sensus.Infrastructure.Persistence.Mongo.Repositories;

/// <summary>
/// Consulta de visibilidad sobre la colección 'feed_posts' sin acoplarse a entidades de Spec 024.
/// </summary>
public sealed class MongoFeedPostVisibilityLookup : IFeedPostVisibilityLookup
{
    private readonly MongoDbContext _context;

    public MongoFeedPostVisibilityLookup(MongoDbContext context)
    {
        _context = context;
    }

    public async Task<FeedPostLookupResult> GetVisibilityAsync(
        Guid postId,
        CancellationToken cancellationToken = default)
    {
        IMongoCollection<BsonDocument> collection = _context.FeedPostsRaw;
        FilterDefinition<BsonDocument> filter = Builders<BsonDocument>.Filter.Eq("_id", postId.ToString());

        BsonDocument? doc = await collection
            .Find(filter)
            .FirstOrDefaultAsync(cancellationToken);

        if (doc is null)
        {
            return new FeedPostLookupResult(Exists: false, IsVisible: false, AuthorUserId: null);
        }

        Guid? authorId = TryReadGuid(doc, "authorUserId") ?? TryReadGuid(doc, "userId");

        return new FeedPostLookupResult(
            Exists: true,
            IsVisible: IsVisible(doc),
            AuthorUserId: authorId);
    }

    /// <summary>
    /// Lista de permitidos: solo publicaciones con veredicto favorable son visibles (FR-024-02).
    /// </summary>
    public static bool IsVisible(BsonDocument doc)
    {
        bool isDeleted = doc.TryGetValue("isDeleted", out BsonValue deletedVal) && deletedVal.IsBoolean && deletedVal.AsBoolean;
        string status = doc.TryGetValue("status", out BsonValue statusVal) && statusVal.IsString
            ? statusVal.AsString.Trim()
            : FeedPostStatus.PendingModeration.ToString();

        return !isDeleted && status.Equals(FeedPostStatus.Published.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    internal static Guid? TryReadGuid(BsonDocument doc, string fieldName)
    {
        if (!doc.TryGetValue(fieldName, out BsonValue val) || val.IsBsonNull)
        {
            return null;
        }

        if (val.IsString && Guid.TryParse(val.AsString, out Guid parsed))
        {
            return parsed;
        }

        return null;
    }
}
