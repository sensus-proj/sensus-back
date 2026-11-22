using Sensus.Application.Offers.DTOs;
using Sensus.Domain.Enums;
using Sensus.Domain.Models;

namespace Sensus.Application.Offers;

/// <summary>
/// Mapeo explícito de elementos del dominio al contrato DTO del feed (Spec 042).
/// </summary>
public static class FeedMappingExtensions
{
    public static OfferFeedItemDto ToFeedItemDto(this FeedOfferCandidate candidate, FeedSlot slot)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        return new OfferFeedItemDto(
            candidate.OfferId,
            candidate.Title,
            candidate.Category,
            candidate.ImageUrl,
            candidate.OriginalPrice,
            candidate.FinalPrice,
            candidate.Stock,
            Math.Max(0, (int)Math.Round(candidate.DistanceMeters, MidpointRounding.AwayFromZero)),
            DateTime.SpecifyKind(candidate.ExpiresAtUtc, DateTimeKind.Utc),
            candidate.MerchantId,
            candidate.MerchantName,
            slot.ToWireName());
    }
}
