namespace Sensus.Application.Offers.DTOs;

public sealed record FavoriteToggleResultDto(Guid OfferId, bool Favorited, int FavoriteCount);
