using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using Sensus.Domain.Entities;

namespace Sensus.Infrastructure.Persistence.Mongo.Documents;

/// <summary>
/// Documento de activo multimedia en MongoDB Atlas (Spec 022).
/// </summary>
public sealed class MediaAssetDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public Guid Id { get; set; }

    [BsonElement("ownerId")]
    [BsonRepresentation(BsonType.String)]
    public Guid OwnerId { get; set; }

    [BsonElement("publicId")]
    public string PublicId { get; set; } = string.Empty;

    [BsonElement("secureUrl")]
    public string? SecureUrl { get; set; }

    [BsonElement("contentType")]
    public string ContentType { get; set; } = string.Empty;

    [BsonElement("status")]
    public int Status { get; set; }

    [BsonElement("createdAtUtc")]
    public DateTime CreatedAtUtc { get; set; }

    public static MediaAssetDocument FromDomain(MediaAsset mediaAsset)
    {
        return new MediaAssetDocument
        {
            Id = mediaAsset.Id,
            OwnerId = mediaAsset.OwnerId,
            PublicId = mediaAsset.PublicId,
            SecureUrl = mediaAsset.SecureUrl,
            ContentType = mediaAsset.ContentType,
            Status = (int)mediaAsset.Status,
            CreatedAtUtc = mediaAsset.CreatedAtUtc
        };
    }

    public MediaAsset ToDomain()
    {
        var mediaAsset = MediaAsset.Create(
            OwnerId,
            PublicId,
            SecureUrl,
            ContentType,
            (MediaAssetStatus)Status,
            CreatedAtUtc);

        // Restaurar el Id persistido
        typeof(MediaAsset).GetProperty("Id")?.SetValue(mediaAsset, Id);

        return mediaAsset;
    }
}
