namespace Sensus.Application.Offers.DTOs;

/// <summary>
/// Estado actualizado de "Me gusta" para una publicación de producto.
/// </summary>
public sealed record OfferLikeStatusDto(
    Guid OfferId,
    int LikesCount,
    bool IsLikedByMe);
