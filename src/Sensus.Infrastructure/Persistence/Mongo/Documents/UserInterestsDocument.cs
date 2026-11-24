using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using Sensus.Domain.Entities;
using Sensus.Domain.Enums;

namespace Sensus.Infrastructure.Persistence.Mongo.Documents;

/// <summary>
/// Documento MongoDB para la colección <c>user_interests</c> (Spec 054).
/// </summary>
public sealed class UserInterestsDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public Guid Id { get; set; }

    [BsonRepresentation(BsonType.String)]
    [BsonElement("userId")]
    public Guid UserId { get; set; }

    [BsonElement("categories")]
    public List<string> Categories { get; set; } = [];

    [BsonElement("updatedAtUtc")]
    public DateTime UpdatedAtUtc { get; set; }

    public UserInterests ToDomain()
    {
        var parsed = new List<OfferCategory>(Categories.Count);
        foreach (string raw in Categories)
        {
            if (OfferCategoryCatalog.TryParse(raw, out OfferCategory category))
            {
                parsed.Add(category);
            }
        }

        return UserInterests.Reconstitute(Id, UserId, parsed, UpdatedAtUtc);
    }

    public static UserInterestsDocument FromDomain(UserInterests interests) =>
        new()
        {
            Id = interests.Id,
            UserId = interests.UserId,
            Categories = interests.Categories.Select(c => c.ToString()).ToList(),
            UpdatedAtUtc = interests.UpdatedAtUtc
        };
}
