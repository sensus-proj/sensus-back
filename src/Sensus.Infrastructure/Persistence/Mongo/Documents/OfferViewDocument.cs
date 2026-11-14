using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using Sensus.Domain.Entities;

namespace Sensus.Infrastructure.Persistence.Mongo.Documents;

/// <summary>
/// Documento de vista de oferta en MongoDB. La clave única usuario + oferta + día impide duplicados
/// (FR-032-03, CA-032-04).
/// </summary>
public sealed class OfferViewDocument
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

    [BsonElement("viewedOn")]
    public DateTime ViewedOn { get; set; }

    [BsonElement("createdAtUtc")]
    public DateTime CreatedAtUtc { get; set; }

    public static OfferViewDocument FromDomain(OfferView view) =>
        new()
        {
            Id = view.Id,
            UserId = view.UserId,
            OfferId = view.OfferId,
            ViewedOn = view.ViewedOn,
            CreatedAtUtc = view.CreatedAtUtc
        };
}
