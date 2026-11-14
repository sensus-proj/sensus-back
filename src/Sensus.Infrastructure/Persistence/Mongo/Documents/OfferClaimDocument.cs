using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using Sensus.Domain.Entities;
using Sensus.Domain.Enums;

namespace Sensus.Infrastructure.Persistence.Mongo.Documents;

/// <summary>
/// Documento de apartado de oferta en MongoDB (spec 033). Solo persiste el hash del código.
/// </summary>
public sealed class OfferClaimDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public Guid Id { get; set; }

    [BsonRepresentation(BsonType.String)]
    [BsonElement("userId")]
    public Guid UserId { get; set; }

    [BsonRepresentation(BsonType.String)]
    [BsonElement("offerId")]
    public Guid OfferId { get; set; }

    [BsonRepresentation(BsonType.String)]
    [BsonElement("merchantId")]
    public Guid MerchantId { get; set; }

    [BsonElement("codeHash")]
    public string CodeHash { get; set; } = string.Empty;

    [BsonElement("status")]
    public string Status { get; set; } = OfferClaimStatus.Active.ToString();

    [BsonElement("expiresAtUtc")]
    public DateTime ExpiresAtUtc { get; set; }

    [BsonElement("redeemedAtUtc")]
    public DateTime? RedeemedAtUtc { get; set; }

    [BsonElement("createdAtUtc")]
    public DateTime CreatedAtUtc { get; set; }

    public static OfferClaimDocument FromDomain(OfferClaim claim)
    {
        return new OfferClaimDocument
        {
            Id = claim.Id,
            UserId = claim.UserId,
            OfferId = claim.OfferId,
            MerchantId = claim.MerchantId,
            CodeHash = claim.CodeHash,
            Status = claim.Status.ToString(),
            ExpiresAtUtc = claim.ExpiresAtUtc,
            RedeemedAtUtc = claim.RedeemedAtUtc,
            CreatedAtUtc = claim.CreatedAtUtc
        };
    }

    public OfferClaim ToDomain()
    {
        OfferClaimStatus status = Enum.TryParse(Status, true, out OfferClaimStatus parsed)
            ? parsed
            : OfferClaimStatus.Active;

        return OfferClaim.Reconstitute(
            Id,
            UserId,
            OfferId,
            MerchantId,
            CodeHash,
            status,
            ExpiresAtUtc,
            RedeemedAtUtc,
            CreatedAtUtc);
    }
}
