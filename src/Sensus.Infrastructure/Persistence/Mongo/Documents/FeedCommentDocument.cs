using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using Sensus.Domain.Entities;

namespace Sensus.Infrastructure.Persistence.Mongo.Documents;

/// <summary>
/// Documento de comentario del feed en MongoDB Atlas (Spec 025).
/// </summary>
public sealed class FeedCommentDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public Guid Id { get; set; }

    [BsonElement("postId")]
    [BsonRepresentation(BsonType.String)]
    public Guid PostId { get; set; }

    [BsonElement("authorUserId")]
    [BsonRepresentation(BsonType.String)]
    public Guid AuthorUserId { get; set; }

    [BsonElement("authorDisplayName")]
    public string AuthorDisplayName { get; set; } = FeedComment.DefaultAuthorDisplayName;

    [BsonElement("authorPhotoUrl")]
    public string? AuthorPhotoUrl { get; set; }

    [BsonElement("text")]
    public string Text { get; set; } = string.Empty;

    [BsonElement("createdAtUtc")]
    public DateTime CreatedAtUtc { get; set; }

    [BsonElement("underReview")]
    public bool UnderReview { get; set; }

    [BsonElement("flaggedAtUtc")]
    public DateTime? FlaggedAtUtc { get; set; }

    public FeedComment ToDomain() =>
        FeedComment.Create(
            PostId,
            AuthorUserId,
            AuthorDisplayName,
            Text,
            CreatedAtUtc,
            Id,
            AuthorPhotoUrl).Value;

    public static FeedCommentDocument FromDomain(FeedComment comment) =>
        new()
        {
            Id = comment.Id,
            PostId = comment.PostId,
            AuthorUserId = comment.AuthorUserId,
            AuthorDisplayName = comment.AuthorDisplayName,
            AuthorPhotoUrl = comment.AuthorPhotoUrl,
            Text = comment.Text,
            CreatedAtUtc = comment.CreatedAtUtc
        };
}
