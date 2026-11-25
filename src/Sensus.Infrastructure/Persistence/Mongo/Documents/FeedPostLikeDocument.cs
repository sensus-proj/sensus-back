using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using Sensus.Domain.Entities;

namespace Sensus.Infrastructure.Persistence.Mongo.Documents;

/// <summary>
/// Documento de me gusta de publicación del feed (Spec 039). Colección <c>feed_post_likes</c>
/// con índice único <c>postId + userId</c> (FR-039-04).
/// </summary>
public sealed class FeedPostLikeDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public Guid Id { get; set; }

    [BsonElement("postId")]
    [BsonRepresentation(BsonType.String)]
    public Guid PostId { get; set; }

    [BsonElement("userId")]
    [BsonRepresentation(BsonType.String)]
    public Guid UserId { get; set; }

    [BsonElement("createdAtUtc")]
    public DateTime CreatedAtUtc { get; set; }

    public FeedPostLike ToDomain() => FeedPostLike.Rehydrate(Id, PostId, UserId, CreatedAtUtc);

    public static FeedPostLikeDocument FromDomain(FeedPostLike like) =>
        new()
        {
            Id = like.Id,
            PostId = like.PostId,
            UserId = like.UserId,
            CreatedAtUtc = like.CreatedAtUtc
        };
}
