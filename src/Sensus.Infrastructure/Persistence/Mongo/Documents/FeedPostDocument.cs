using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver.GeoJsonObjectModel;
using Sensus.Domain.Entities;
using Sensus.Domain.Enums;

namespace Sensus.Infrastructure.Persistence.Mongo.Documents;

/// <summary>
/// Documento de publicación del feed social en MongoDB Atlas (Spec 024).
/// </summary>
public sealed class FeedPostDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public Guid Id { get; set; }

    [BsonElement("authorUserId")]
    [BsonRepresentation(BsonType.String)]
    public Guid AuthorUserId { get; set; }

    [BsonElement("authorDisplayName")]
    public string AuthorDisplayName { get; set; } = FeedPost.DefaultAuthorDisplayName;

    [BsonElement("authorPhotoUrl")]
    public string? AuthorPhotoUrl { get; set; }

    [BsonElement("content")]
    public string Content { get; set; } = string.Empty;

    [BsonElement("imageUrl")]
    public string? ImageUrl { get; set; }

    [BsonElement("imagePublicId")]
    public string? ImagePublicId { get; set; }

    [BsonElement("status")]
    public string Status { get; set; } = FeedPostStatus.Published.ToString();

    [BsonElement("isDeleted")]
    public bool IsDeleted { get; set; }

    [BsonElement("createdAtUtc")]
    public DateTime CreatedAtUtc { get; set; }

    [BsonElement("updatedAtUtc")]
    public DateTime? UpdatedAtUtc { get; set; }

    [BsonElement("deletedAtUtc")]
    public DateTime? DeletedAtUtc { get; set; }

    /// <summary>
    /// Punto GeoJSON [lng, lat] para el feed cercano (Spec 026). Se omite si el autor no compartió ubicación,
    /// de modo que el índice 2dsphere no indexa la publicación (FR-026-05).
    /// </summary>
    [BsonElement("location")]
    [BsonIgnoreIfNull]
    public GeoJsonPoint<GeoJson2DGeographicCoordinates>? Location { get; set; }

    [BsonElement("merchantId")]
    [BsonRepresentation(BsonType.String)]
    [BsonIgnoreIfNull]
    public Guid? MerchantId { get; set; }

    [BsonElement("merchantName")]
    [BsonIgnoreIfNull]
    public string? MerchantName { get; set; }

    public FeedPost ToDomain()
    {
        Enum.TryParse(Status, ignoreCase: true, out FeedPostStatus postStatus);

        return FeedPost.Rehydrate(
            id: Id,
            authorUserId: AuthorUserId,
            authorDisplayName: AuthorDisplayName,
            authorPhotoUrl: AuthorPhotoUrl,
            content: Content,
            imageUrl: ImageUrl,
            status: postStatus,
            imagePublicId: ImagePublicId,
            isDeleted: IsDeleted,
            createdAtUtc: CreatedAtUtc,
            updatedAtUtc: UpdatedAtUtc,
            deletedAtUtc: DeletedAtUtc,
            latitude: Location?.Coordinates.Latitude,
            longitude: Location?.Coordinates.Longitude,
            merchantId: MerchantId,
            merchantName: MerchantName);
    }

    public static FeedPostDocument FromDomain(FeedPost post) =>
        new()
        {
            Id = post.Id,
            AuthorUserId = post.AuthorUserId,
            AuthorDisplayName = post.AuthorDisplayName,
            AuthorPhotoUrl = post.AuthorPhotoUrl,
            Content = post.Content,
            ImageUrl = post.ImageUrl,
            ImagePublicId = post.ImagePublicId,
            Status = post.Status.ToString(),
            IsDeleted = post.IsDeleted,
            CreatedAtUtc = post.CreatedAtUtc,
            UpdatedAtUtc = post.UpdatedAtUtc,
            DeletedAtUtc = post.DeletedAtUtc,
            Location = post.HasLocation
                ? new GeoJsonPoint<GeoJson2DGeographicCoordinates>(
                    new GeoJson2DGeographicCoordinates(post.Longitude!.Value, post.Latitude!.Value))
                : null,
            MerchantId = post.MerchantId,
            MerchantName = post.MerchantName
        };
}
