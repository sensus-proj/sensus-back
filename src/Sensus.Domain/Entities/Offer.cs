using Sensus.Domain.Common;
using Sensus.Domain.Enums;

namespace Sensus.Domain.Entities;

/// <summary>
/// Raíz de Agregado Offer que modela las ofertas de un comercio, su control automático de vigencia y republicación (SE-022, SE-026, Specs 034 y 036).
/// </summary>
public class Offer : BaseAuditableEntity, IAggregateRoot
{
    public Guid MerchantId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public decimal OriginalPrice { get; private set; }
    public int DiscountPercentage { get; private set; }
    public decimal DiscountedPrice { get; private set; }
    public int Stock { get; private set; }
    public DateTime StartsAt { get; private set; }
    public DateTime EndsAt { get; private set; }
    public OfferStatus Status { get; private set; }
    public string? ImageUrl { get; private set; }
    public double? Latitude { get; private set; }
    public double? Longitude { get; private set; }

    // Constructor privado para persistencia / deserialización
    private Offer() : base()
    {
    }

    private Offer(
        Guid id,
        Guid merchantId,
        string title,
        string description,
        decimal originalPrice,
        int discountPercentage,
        decimal discountedPrice,
        int stock,
        DateTime startsAt,
        DateTime endsAt,
        OfferStatus status,
        string? imageUrl,
        double? latitude,
        double? longitude,
        DateTime createdAtUtc) : base(id)
    {
        MerchantId = merchantId;
        Title = title;
        Description = description;
        OriginalPrice = originalPrice;
        DiscountPercentage = discountPercentage;
        DiscountedPrice = discountedPrice;
        Stock = stock;
        StartsAt = startsAt;
        EndsAt = endsAt;
        Status = status;
        ImageUrl = imageUrl;
        Latitude = latitude;
        Longitude = longitude;
        CreatedAtUtc = createdAtUtc;
    }

    /// <summary>
    /// Factory method para crear una nueva oferta aplicando las invariantes de negocio (SE-022 y FR-034-02).
    /// </summary>
    public static Result<Offer> Create(
        Guid merchantId,
        string title,
        string description,
        decimal originalPrice,
        int discountPercentage,
        int stock,
        DateTime startsAt,
        DateTime endsAt,
        string? imageUrl = null,
        double? latitude = null,
        double? longitude = null,
        DateTime? nowUtc = null)
    {
        DateTime now = nowUtc ?? DateTime.UtcNow;

        if (merchantId == Guid.Empty)
        {
            return Result.Failure<Offer>(Error.Validation("Offer.MerchantIdRequired", "El identificador del comercio es obligatorio."));
        }

        if (string.IsNullOrWhiteSpace(title) || title.Trim().Length is < 3 or > 100)
        {
            return Result.Failure<Offer>(Error.Validation("Offer.InvalidTitle", "El título de la oferta debe tener entre 3 y 100 caracteres."));
        }

        if (string.IsNullOrWhiteSpace(description) || description.Trim().Length is < 5 or > 500)
        {
            return Result.Failure<Offer>(Error.Validation("Offer.InvalidDescription", "La descripción de la oferta debe tener entre 5 y 500 caracteres."));
        }

        if (originalPrice <= 0)
        {
            return Result.Failure<Offer>(Error.Validation("Offer.InvalidOriginalPrice", "El precio original debe ser mayor a 0."));
        }

        if (discountPercentage is < 1 or > 90)
        {
            return Result.Failure<Offer>(Error.Validation("Offer.InvalidDiscount", "El porcentaje de descuento debe estar entre 1% y 90%."));
        }

        if (stock < 1)
        {
            return Result.Failure<Offer>(Error.Validation("Offer.InvalidStock", "El stock de la oferta debe ser de al menos 1 unidad."));
        }

        if (endsAt <= startsAt)
        {
            return Result.Failure<Offer>(Error.Validation("Offer.InvalidValidityDates", "La fecha de fin de vigencia debe ser posterior a la fecha de inicio."));
        }

        if (endsAt <= now)
        {
            return Result.Failure<Offer>(Error.Validation("Offer.ExpiredEndDate", "La fecha de vencimiento debe estar en el futuro."));
        }

        decimal discountedPrice = Math.Round(originalPrice * (1 - (discountPercentage / 100m)), 2);

        Offer offer = new(
            Guid.NewGuid(),
            merchantId,
            title.Trim(),
            description.Trim(),
            originalPrice,
            discountPercentage,
            discountedPrice,
            stock,
            startsAt.ToUniversalTime(),
            endsAt.ToUniversalTime(),
            OfferStatus.Published,
            imageUrl?.Trim(),
            latitude,
            longitude,
            now);

        return Result.Success(offer);
    }

    /// <summary>
    /// Reconstituye una entidad Offer desde la base de datos (MongoDB).
    /// </summary>
    public static Offer Reconstitute(
        Guid id,
        Guid merchantId,
        string title,
        string description,
        decimal originalPrice,
        int discountPercentage,
        decimal discountedPrice,
        int stock,
        DateTime startsAt,
        DateTime endsAt,
        OfferStatus status,
        string? imageUrl,
        double? latitude,
        double? longitude,
        DateTime createdAtUtc,
        DateTime? lastModifiedAtUtc = null)
    {
        Offer offer = new(
            id,
            merchantId,
            title,
            description,
            originalPrice,
            discountPercentage,
            discountedPrice,
            stock,
            startsAt,
            endsAt,
            status,
            imageUrl,
            latitude,
            longitude,
            createdAtUtc)
        {
            LastModifiedAtUtc = lastModifiedAtUtc
        };

        return offer;
    }

    /// <summary>
    /// Control automático de vigencia: marca la oferta como expirada si se alcanzó la fecha y hora de vencimiento (SE-022 a SE-025).
    /// </summary>
    public Result Expire(DateTime nowUtc)
    {
        if (Status == OfferStatus.Expired)
        {
            return Result.Success();
        }

        if (nowUtc < EndsAt)
        {
            return Result.Failure(Error.Conflict("Offer.NotYetExpired", "La oferta aún no ha alcanzado su fecha de expiración."));
        }

        Status = OfferStatus.Expired;
        LastModifiedAtUtc = nowUtc;
        return Result.Success();
    }

    /// <summary>
    /// Marca una oferta pasada (Expirada o Archivada) para republicarla con nuevas fechas y stock (SE-026 y FR-036-05).
    /// </summary>
    public Result Republish(DateTime newStartsAt, DateTime newEndsAt, int newStock, DateTime nowUtc)
    {
        if (Status is not (OfferStatus.Expired or OfferStatus.Archived))
        {
            return Result.Failure(Error.Conflict("Offer.CannotRepublishActive", "Solo se pueden republicar ofertas expiradas o archivadas."));
        }

        if (newEndsAt <= newStartsAt)
        {
            return Result.Failure(Error.Validation("Offer.InvalidValidityDates", "La nueva fecha de fin debe ser posterior a la fecha de inicio."));
        }

        if (newEndsAt <= nowUtc)
        {
            return Result.Failure(Error.Validation("Offer.ExpiredEndDate", "La nueva fecha de vencimiento debe estar en el futuro."));
        }

        if (newStock < 1)
        {
            return Result.Failure(Error.Validation("Offer.InvalidStock", "El nuevo stock debe ser de al menos 1 unidad."));
        }

        StartsAt = newStartsAt.ToUniversalTime();
        EndsAt = newEndsAt.ToUniversalTime();
        Stock = newStock;
        Status = OfferStatus.Published;
        LastModifiedAtUtc = nowUtc;

        return Result.Success();
    }

    /// <summary>
    /// Pausa temporalmente la visibilidad de la oferta.
    /// </summary>
    public Result Pause(DateTime nowUtc)
    {
        if (Status != OfferStatus.Published)
        {
            return Result.Failure(Error.Conflict("Offer.CannotPause", "Solo se pueden pausar ofertas publicadas."));
        }

        Status = OfferStatus.Paused;
        LastModifiedAtUtc = nowUtc;
        return Result.Success();
    }

    /// <summary>
    /// Reactiva una oferta pausada si sigue vigente.
    /// </summary>
    public Result Resume(DateTime nowUtc)
    {
        if (Status != OfferStatus.Paused)
        {
            return Result.Failure(Error.Conflict("Offer.CannotResume", "Solo se pueden reanudar ofertas pausadas."));
        }

        if (nowUtc >= EndsAt)
        {
            Status = OfferStatus.Expired;
            LastModifiedAtUtc = nowUtc;
            return Result.Failure(Error.Conflict("Offer.OfferExpired", "La oferta no se puede reanudar porque su vigencia ya expiró."));
        }

        Status = OfferStatus.Published;
        LastModifiedAtUtc = nowUtc;
        return Result.Success();
    }

    /// <summary>
    /// Archiva definitivamente la oferta.
    /// </summary>
    public Result Archive(DateTime nowUtc)
    {
        Status = OfferStatus.Archived;
        LastModifiedAtUtc = nowUtc;
        return Result.Success();
    }

    /// <summary>
    /// Indica si la oferta se encuentra vigente a una fecha dada.
    /// </summary>
    public bool IsActive(DateTime nowUtc) =>
        Status == OfferStatus.Published && nowUtc >= StartsAt && nowUtc < EndsAt && Stock > 0;
}
