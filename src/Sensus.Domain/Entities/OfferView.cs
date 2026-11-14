using Sensus.Domain.Common;

namespace Sensus.Domain.Entities;

/// <summary>
/// Vista de una oferta por un usuario en un día concreto. La combinación
/// usuario + oferta + día es única y evita inflar el contador (FR-032-03, FR-032-04 y FR-032-06).
/// </summary>
public class OfferView : BaseEntity
{
    public Guid UserId { get; private set; }
    public Guid OfferId { get; private set; }

    /// <summary>Día UTC de la vista, normalizado a medianoche para la clave de deduplicación.</summary>
    public DateTime ViewedOn { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    private OfferView() : base()
    {
    }

    private OfferView(Guid id, Guid userId, Guid offerId, DateTime viewedOn, DateTime createdAtUtc) : base(id)
    {
        UserId = userId;
        OfferId = offerId;
        ViewedOn = viewedOn;
        CreatedAtUtc = createdAtUtc;
    }

    /// <summary>
    /// Crea la vista de un usuario sobre una oferta, normalizando el día a medianoche UTC.
    /// </summary>
    public static Result<OfferView> Create(Guid userId, Guid offerId, DateTime nowUtc)
    {
        if (userId == Guid.Empty)
        {
            return Result.Failure<OfferView>(
                Error.Validation("OfferView.UserRequired", "El identificador del usuario es obligatorio."));
        }

        if (offerId == Guid.Empty)
        {
            return Result.Failure<OfferView>(
                Error.Validation("OfferView.OfferRequired", "El identificador de la oferta es obligatorio."));
        }

        DateTime createdAt = nowUtc.ToUniversalTime();
        DateTime viewedOn = createdAt.Date;

        return Result.Success(new OfferView(Guid.NewGuid(), userId, offerId, viewedOn, createdAt));
    }
}
