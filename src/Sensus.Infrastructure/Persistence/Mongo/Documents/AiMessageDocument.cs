using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using Sensus.Domain.Entities;
using Sensus.Domain.Enums;
using Sensus.Domain.ValueObjects;

namespace Sensus.Infrastructure.Persistence.Mongo.Documents;

public sealed class AiMessageDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public Guid Id { get; set; }

    [BsonRepresentation(BsonType.String)]
    [BsonElement("conversationId")]
    public Guid ConversationId { get; set; }

    [BsonRepresentation(BsonType.String)]
    [BsonElement("ownerUserId")]
    public Guid OwnerUserId { get; set; }

    [BsonElement("role")]
    public string Role { get; set; } = AiMessageRole.User.ToString();

    [BsonElement("redactedContent")]
    public string RedactedContent { get; set; } = string.Empty;

    [BsonElement("degraded")]
    public bool Degraded { get; set; }

    [BsonElement("degradationReason")]
    public string? DegradationReason { get; set; }

    [BsonElement("recommendedPackage")]
    public RecommendedPackageDocument? RecommendedPackage { get; set; }

    [BsonElement("offersCitedIds")]
    public List<string> OffersCitedIds { get; set; } = new();

    [BsonElement("toolsInvoked")]
    public List<string> ToolsInvoked { get; set; } = new();

    [BsonElement("createdAtUtc")]
    public DateTime CreatedAtUtc { get; set; }

    public static AiMessageDocument FromDomain(AiMessage entity) => new()
    {
        Id = entity.Id,
        ConversationId = entity.ConversationId,
        OwnerUserId = entity.OwnerUserId,
        Role = entity.Role.ToString(),
        RedactedContent = entity.RedactedContent,
        Degraded = entity.Degraded,
        DegradationReason = entity.DegradationReason,
        RecommendedPackage = entity.RecommendedPackage is not null
            ? RecommendedPackageDocument.FromDomain(entity.RecommendedPackage)
            : null,
        OffersCitedIds = entity.OffersCitedIds.ToList(),
        ToolsInvoked = entity.ToolsInvoked.ToList(),
        CreatedAtUtc = entity.CreatedAtUtc
    };

    public AiMessage ToDomain()
    {
        var role = Enum.TryParse<AiMessageRole>(Role, out var r) ? r : AiMessageRole.User;
        var pkg = RecommendedPackage?.ToDomain();

        return AiMessage.Reconstitute(
            Id,
            ConversationId,
            OwnerUserId,
            role,
            RedactedContent,
            Degraded,
            DegradationReason,
            pkg,
            OffersCitedIds.AsReadOnly(),
            ToolsInvoked.AsReadOnly(),
            CreatedAtUtc);
    }
}

public sealed class RecommendedPackageDocument
{
    [BsonElement("budgetMxn")]
    [BsonRepresentation(BsonType.Decimal128)]
    public decimal BudgetMxn { get; set; }

    [BsonElement("totalPriceMxn")]
    [BsonRepresentation(BsonType.Decimal128)]
    public decimal TotalPriceMxn { get; set; }

    [BsonElement("savingsMxn")]
    [BsonRepresentation(BsonType.Decimal128)]
    public decimal SavingsMxn { get; set; }

    [BsonElement("items")]
    public List<RecommendedPackageItemDocument> Items { get; set; } = new();

    public static RecommendedPackageDocument FromDomain(RecommendedPackage pkg) => new()
    {
        BudgetMxn = pkg.BudgetMxn,
        TotalPriceMxn = pkg.TotalPriceMxn,
        SavingsMxn = pkg.SavingsMxn,
        Items = pkg.Items.Select(RecommendedPackageItemDocument.FromDomain).ToList()
    };

    public RecommendedPackage? ToDomain()
    {
        var domainItems = Items.Select(i => i.ToDomain()).ToList();
        var result = Sensus.Domain.ValueObjects.RecommendedPackage.Create(BudgetMxn, domainItems);
        return result.IsSuccess ? result.Value : null;
    }
}

public sealed class RecommendedPackageItemDocument
{
    [BsonElement("offerId")]
    public string OfferId { get; set; } = string.Empty;

    [BsonElement("title")]
    public string Title { get; set; } = string.Empty;

    [BsonElement("merchantId")]
    public string MerchantId { get; set; } = string.Empty;

    [BsonElement("merchantName")]
    public string MerchantName { get; set; } = string.Empty;

    [BsonElement("offerPriceMxn")]
    [BsonRepresentation(BsonType.Decimal128)]
    public decimal OfferPriceMxn { get; set; }

    [BsonElement("regularPriceMxn")]
    [BsonRepresentation(BsonType.Decimal128)]
    public decimal RegularPriceMxn { get; set; }

    [BsonElement("availableStock")]
    public int AvailableStock { get; set; }

    [BsonElement("isOpenNow")]
    public bool IsOpenNow { get; set; }

    [BsonElement("distanceKm")]
    public double? DistanceKm { get; set; }

    public static RecommendedPackageItemDocument FromDomain(RecommendedPackageItem item) => new()
    {
        OfferId = item.OfferId,
        Title = item.Title,
        MerchantId = item.MerchantId,
        MerchantName = item.MerchantName,
        OfferPriceMxn = item.OfferPriceMxn,
        RegularPriceMxn = item.RegularPriceMxn,
        AvailableStock = item.AvailableStock,
        IsOpenNow = item.IsOpenNow,
        DistanceKm = item.DistanceKm
    };

    public RecommendedPackageItem ToDomain() => new(
        OfferId,
        Title,
        MerchantId,
        MerchantName,
        OfferPriceMxn,
        RegularPriceMxn,
        AvailableStock,
        IsOpenNow,
        DistanceKm);
}
