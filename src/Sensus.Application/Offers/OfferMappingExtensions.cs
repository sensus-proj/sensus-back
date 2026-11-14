using Sensus.Application.Offers.DTOs;
using Sensus.Domain.Entities;

namespace Sensus.Application.Offers;

public static class OfferMappingExtensions
{
    public static OfferDto ToDto(this Offer offer)
    {
        return new OfferDto(
            offer.Id,
            offer.MerchantId,
            offer.Title,
            offer.Description,
            offer.OriginalPrice,
            offer.DiscountPercentage,
            offer.DiscountedPrice,
            offer.Stock,
            offer.StartsAt,
            offer.EndsAt,
            offer.Status.ToString(),
            offer.ImageUrl,
            offer.Latitude,
            offer.Longitude,
            offer.CreatedAtUtc,
            offer.LastModifiedAtUtc,
            offer.Version,
            offer.Category);
    }
}
