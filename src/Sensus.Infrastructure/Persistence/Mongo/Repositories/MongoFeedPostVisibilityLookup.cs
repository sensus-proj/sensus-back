using MongoDB.Bson;
using MongoDB.Driver;
using Sensus.Application.Common.Interfaces;

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

        bool isDeleted = doc.TryGetValue("isDeleted", out BsonValue deletedVal) && deletedVal.IsBoolean && deletedVal.AsBoolean;
        string status = doc.TryGetValue("status", out BsonValue statusVal) && statusVal.IsString
            ? statusVal.AsString.Trim()
            : "Published";

        bool statusBlocked = status.Equals("Hidden", StringComparison.OrdinalIgnoreCase)
                             || status.Equals("Rejected", StringComparison.OrdinalIgnoreCase)
                             || status.Equals("Deleted", StringComparison.OrdinalIgnoreCase)
                             || status.Equals("Pending", StringComparison.OrdinalIgnoreCase);

        Guid? authorId = TryReadGuid(doc, "authorUserId") ?? TryReadGuid(doc, "userId");

        return new FeedPostLookupResult(
            Exists: true,
            IsVisible: !isDeleted && !statusBlocked,
            AuthorUserId: authorId);
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
