using Sensus.Domain.Common;

namespace Sensus.Domain.Entities;

/// <summary>
/// Oferta compartida directamente entre dos usuarios que son amigos en Sensus.
/// </summary>
public sealed class SharedOffer : BaseEntity, IAggregateRoot
{
    public Guid SenderUserId { get; private set; }
    public Guid RecipientUserId { get; private set; }
    public Guid OfferId { get; private set; }
    public string OfferTitle { get; private set; } = string.Empty;
    public string StoreName { get; private set; } = string.Empty;
    public Guid? MerchantId { get; private set; }
    public string? Category { get; private set; }
    public decimal OriginalPrice { get; private set; }
    public decimal DiscountedPrice { get; private set; }
    public int DiscountPercentage { get; private set; }
    public string? ImageUrl { get; private set; }
    public DateTime SharedAtUtc { get; private set; }

    private SharedOffer() : base()
    {
    }

    private SharedOffer(
        Guid id,
        Guid senderUserId,
        Guid recipientUserId,
        Guid offerId,
        string offerTitle,
        string storeName,
        Guid? merchantId,
        string? category,
        decimal originalPrice,
        decimal discountedPrice,
        int discountPercentage,
        string? imageUrl,
        DateTime sharedAtUtc) : base(id)
    {
        SenderUserId = senderUserId;
        RecipientUserId = recipientUserId;
        OfferId = offerId;
        OfferTitle = offerTitle;
        StoreName = storeName;
        MerchantId = merchantId;
        Category = category;
        OriginalPrice = originalPrice;
        DiscountedPrice = discountedPrice;
        DiscountPercentage = discountPercentage;
        ImageUrl = imageUrl;
        SharedAtUtc = sharedAtUtc;
    }

    public static Result<SharedOffer> Create(
        Guid senderUserId,
        Guid recipientUserId,
        Guid offerId,
        string offerTitle,
        string storeName,
        Guid? merchantId,
        string? category,
        decimal originalPrice,
        decimal discountedPrice,
        int discountPercentage,
        string? imageUrl,
        DateTime sharedAtUtc)
    {
        if (senderUserId == Guid.Empty || recipientUserId == Guid.Empty || offerId == Guid.Empty)
        {
            return Result.Failure<SharedOffer>(
                Error.Validation("FriendShare.InvalidIds", "Los identificadores para compartir oferta son obligatorios."));
        }

        if (senderUserId == recipientUserId)
        {
            return Result.Failure<SharedOffer>(
                Error.Validation("FriendShare.SelfShare", "No puedes compartir una oferta contigo mismo."));
        }

        if (string.IsNullOrWhiteSpace(offerTitle))
        {
            return Result.Failure<SharedOffer>(
                Error.Validation("FriendShare.EmptyTitle", "El título de la oferta es obligatorio."));
        }

        return Result.Success(new SharedOffer(
            Guid.NewGuid(),
            senderUserId,
            recipientUserId,
            offerId,
            offerTitle.Trim(),
            string.IsNullOrWhiteSpace(storeName) ? "Comercio local" : storeName.Trim(),
            merchantId,
            category,
            originalPrice,
            discountedPrice,
            discountPercentage,
            imageUrl,
            sharedAtUtc));
    }

    public static SharedOffer Rehydrate(
        Guid id,
        Guid senderUserId,
        Guid recipientUserId,
        Guid offerId,
        string offerTitle,
        string storeName,
        Guid? merchantId,
        string? category,
        decimal originalPrice,
        decimal discountedPrice,
        int discountPercentage,
        string? imageUrl,
        DateTime sharedAtUtc)
    {
        return new SharedOffer(
            id,
            senderUserId,
            recipientUserId,
            offerId,
            offerTitle,
            storeName,
            merchantId,
            category,
            originalPrice,
            discountedPrice,
            discountPercentage,
            imageUrl,
            sharedAtUtc);
    }
}
