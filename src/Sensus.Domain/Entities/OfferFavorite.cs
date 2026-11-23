using Sensus.Domain.Common;

namespace Sensus.Domain.Entities;

/// <summary>
/// Marca de oferta favorita guardada por un usuario (Spec 038).
/// </summary>
public sealed class OfferFavorite : BaseEntity
{
    public Guid UserId { get; private set; }
    public Guid OfferId { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    private OfferFavorite() : base()
    {
    }

    private OfferFavorite(Guid id, Guid userId, Guid offerId, DateTime createdAtUtc) : base(id)
    {
        UserId = userId;
        OfferId = offerId;
        CreatedAtUtc = createdAtUtc;
    }

    public static Result<OfferFavorite> Create(Guid userId, Guid offerId, DateTime nowUtc)
    {
        if (userId == Guid.Empty)
        {
            return Result.Failure<OfferFavorite>(
                Error.Validation("OfferFavorite.InvalidUser", "El identificador del usuario es obligatorio."));
        }

        if (offerId == Guid.Empty)
        {
            return Result.Failure<OfferFavorite>(
                Error.Validation("OfferFavorite.InvalidOffer", "El identificador de la oferta es obligatorio."));
        }

        return Result.Success(new OfferFavorite(Guid.NewGuid(), userId, offerId, nowUtc));
    }

    public static OfferFavorite Rehydrate(Guid id, Guid userId, Guid offerId, DateTime createdAtUtc) =>
        new(id, userId, offerId, createdAtUtc);
}
