using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using Sensus.Domain.Entities;

namespace Sensus.Infrastructure.Persistence.Mongo.Documents;

public sealed class StoreReviewReplySubDocument
{
    [BsonRepresentation(BsonType.String)]
    [BsonElement("id")]
    public Guid Id { get; set; }

    [BsonRepresentation(BsonType.String)]
    [BsonElement("authorUserId")]
    public Guid AuthorUserId { get; set; }

    [BsonElement("authorName")]
    public string AuthorName { get; set; } = string.Empty;

    [BsonElement("authorPhotoUrl")]
    public string? AuthorPhotoUrl { get; set; }

    [BsonElement("comment")]
    public string Comment { get; set; } = string.Empty;

    [BsonElement("createdAtUtc")]
    public DateTime CreatedAtUtc { get; set; }

    public StoreReviewReply ToDomain() =>
        StoreReviewReply.Rehydrate(Id, AuthorUserId, AuthorName, AuthorPhotoUrl, Comment, CreatedAtUtc);

    public static StoreReviewReplySubDocument FromDomain(StoreReviewReply reply) =>
        new()
        {
            Id = reply.Id,
            AuthorUserId = reply.AuthorUserId,
            AuthorName = reply.AuthorName,
            AuthorPhotoUrl = reply.AuthorPhotoUrl,
            Comment = reply.Comment,
            CreatedAtUtc = reply.CreatedAtUtc,
        };
}

public sealed class StoreReviewDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public Guid Id { get; set; }

    [BsonRepresentation(BsonType.String)]
    [BsonElement("merchantId")]
    public Guid MerchantId { get; set; }

    [BsonRepresentation(BsonType.String)]
    [BsonElement("authorUserId")]
    public Guid AuthorUserId { get; set; }

    [BsonElement("authorName")]
    public string AuthorName { get; set; } = string.Empty;

    [BsonElement("authorPhotoUrl")]
    public string? AuthorPhotoUrl { get; set; }

    [BsonElement("rating")]
    public int Rating { get; set; }

    [BsonElement("comment")]
    public string Comment { get; set; } = string.Empty;

    [BsonElement("createdAtUtc")]
    public DateTime CreatedAtUtc { get; set; }

    [BsonRepresentation(BsonType.String)]
    [BsonElement("likedByUserIds")]
    public List<Guid> LikedByUserIds { get; set; } = [];

    [BsonElement("replies")]
    public List<StoreReviewReplySubDocument> Replies { get; set; } = [];

    public StoreReview ToDomain() =>
        StoreReview.Rehydrate(
            Id,
            MerchantId,
            AuthorUserId,
            AuthorName,
            AuthorPhotoUrl,
            Rating,
            Comment,
            CreatedAtUtc,
            LikedByUserIds,
            Replies.Select(r => r.ToDomain()));

    public static StoreReviewDocument FromDomain(StoreReview review) =>
        new()
        {
            Id = review.Id,
            MerchantId = review.MerchantId,
            AuthorUserId = review.AuthorUserId,
            AuthorName = review.AuthorName,
            AuthorPhotoUrl = review.AuthorPhotoUrl,
            Rating = review.Rating,
            Comment = review.Comment,
            CreatedAtUtc = review.CreatedAtUtc,
            LikedByUserIds = review.LikedByUserIds.ToList(),
            Replies = review.Replies.Select(StoreReviewReplySubDocument.FromDomain).ToList(),
        };
}
