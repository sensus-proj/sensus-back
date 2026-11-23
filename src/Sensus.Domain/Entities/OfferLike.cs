using Sensus.Domain.Common;

namespace Sensus.Domain.Entities;

/// <summary>
/// Registro único de "Me gusta" de un usuario hacia una publicación de oferta/producto.
/// </summary>
public sealed class OfferLike : BaseEntity
{
    public Guid UserId { get; private set; }
    public Guid OfferId { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    private OfferLike() : base()
    {
    }

    private OfferLike(Guid id, Guid userId, Guid offerId, DateTime createdAtUtc) : base(id)
    {
        UserId = userId;
        OfferId = offerId;
        CreatedAtUtc = createdAtUtc;
    }

    public static Result<OfferLike> Create(Guid userId, Guid offerId, DateTime nowUtc)
    {
        if (userId == Guid.Empty)
        {
            return Result.Failure<OfferLike>(
                Error.Validation("OfferLike.UserRequired", "El usuario es obligatorio para dar me gusta."));
        }

        if (offerId == Guid.Empty)
        {
            return Result.Failure<OfferLike>(
                Error.Validation("OfferLike.OfferRequired", "La publicación es obligatoria para dar me gusta."));
        }

        return Result.Success(new OfferLike(Guid.NewGuid(), userId, offerId, nowUtc.ToUniversalTime()));
    }

    public static OfferLike Rehydrate(Guid id, Guid userId, Guid offerId, DateTime createdAtUtc) =>
        new(id, userId, offerId, createdAtUtc);
}
