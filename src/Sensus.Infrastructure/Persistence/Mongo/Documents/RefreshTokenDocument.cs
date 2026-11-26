using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using RefreshTokenEntity = Sensus.Domain.Entities.RefreshToken;

namespace Sensus.Infrastructure.Persistence.Mongo.Documents;

/// <summary>
/// Documento MongoDB para refresh tokens rotatorios con expiración automática por TTL (Spec 002 / G-06).
/// </summary>
public sealed class RefreshTokenDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public Guid Id { get; set; }

    [BsonElement("userId")]
    [BsonRepresentation(BsonType.String)]
    public Guid UserId { get; set; }

    [BsonElement("familyId")]
    [BsonRepresentation(BsonType.String)]
    public Guid FamilyId { get; set; }

    [BsonElement("tokenHash")]
    public string TokenHash { get; set; } = string.Empty;

    [BsonElement("createdAtUtc")]
    public DateTime CreatedAtUtc { get; set; }

    [BsonElement("expiresAtUtc")]
    public DateTime ExpiresAtUtc { get; set; }

    [BsonElement("usedAtUtc")]
    [BsonIgnoreIfNull]
    public DateTime? UsedAtUtc { get; set; }

    [BsonElement("revokedAtUtc")]
    [BsonIgnoreIfNull]
    public DateTime? RevokedAtUtc { get; set; }

    [BsonElement("revocationReason")]
    [BsonIgnoreIfNull]
    public string? RevocationReason { get; set; }

    [BsonElement("replacedByTokenId")]
    [BsonRepresentation(BsonType.String)]
    [BsonIgnoreIfNull]
    public Guid? ReplacedByTokenId { get; set; }

    public RefreshTokenEntity ToDomain() =>
        RefreshTokenEntity.Reconstitute(
            Id,
            UserId,
            FamilyId,
            TokenHash,
            CreatedAtUtc,
            ExpiresAtUtc,
            UsedAtUtc,
            RevokedAtUtc,
            RevocationReason,
            ReplacedByTokenId);

    public static RefreshTokenDocument FromDomain(RefreshTokenEntity token) =>
        new()
        {
            Id = token.Id,
            UserId = token.UserId,
            FamilyId = token.FamilyId,
            TokenHash = token.TokenHash,
            CreatedAtUtc = token.CreatedAtUtc,
            ExpiresAtUtc = token.ExpiresAtUtc,
            UsedAtUtc = token.UsedAtUtc,
            RevokedAtUtc = token.RevokedAtUtc,
            RevocationReason = token.RevocationReason,
            ReplacedByTokenId = token.ReplacedByTokenId
        };
}
