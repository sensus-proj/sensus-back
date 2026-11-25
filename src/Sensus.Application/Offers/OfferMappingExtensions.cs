using Sensus.Application.Offers.DTOs;
using Sensus.Domain.Entities;
using Sensus.Domain.Models;

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

    /// <summary>
    /// Proyecta una oferta al DTO de inventario con métricas en tiempo real (Spec 037, FR-037-03).
    /// Las vistas se leen del contador de la oferta; los apartados se entregan por lote desde el repositorio.
    /// </summary>
    public static MerchantOfferDto ToMerchantOfferDto(this Offer offer, OfferClaimCounts? claimCounts = null)
    {
        OfferDto dto = offer.ToDto();
        OfferClaimCounts metrics = claimCounts ?? OfferClaimCounts.Empty;

        return new MerchantOfferDto(
            dto.Id,
            dto.MerchantId,
            dto.Title,
            dto.Description,
            dto.OriginalPrice,
            dto.DiscountPercentage,
            dto.DiscountedPrice,
            dto.Stock,
            dto.StartsAt,
            dto.EndsAt,
            dto.Status,
            dto.ImageUrl,
            dto.Latitude,
            dto.Longitude,
            dto.CreatedAtUtc,
            dto.LastModifiedAtUtc,
            dto.Version,
            dto.Category,
            dto.LikesCount,
            dto.IsLikedByMe,
            metrics.ActiveClaims,
            metrics.Redemptions,
            offer.ViewCount);
    }
}
