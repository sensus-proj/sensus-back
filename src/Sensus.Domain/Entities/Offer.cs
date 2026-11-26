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
    public string? Category { get; private set; }

    /// <summary>Contador acumulado de vistas deduplicadas por usuario y día (FR-032-03).</summary>
    public int ViewCount { get; private set; }

    /// <summary>Versión de la entidad para control de concurrencia optimista (FR-035-05).</summary>
    public int Version { get; private set; } = 1;

    /// <summary>Reintentos acumulados de moderación diferida (Spec 034, FR-034-07).</summary>
    public int ModerationRetryCount { get; private set; }

    /// <summary>Próximo instante UTC elegible para re-moderar si está en PendingModeration.</summary>
    public DateTime? NextModerationRetryAtUtc { get; private set; }

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
        DateTime createdAtUtc,
        string? category = null) : base(id)
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
        Category = category;
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
        DateTime? nowUtc = null,
        string? category = null,
        bool requireValidCategory = false,
        bool saveAsDraft = false)
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

        Result<string?> categoryResult = ResolveCategoryForCreate(category, requireValidCategory);
        if (categoryResult.IsFailure)
        {
            return Result.Failure<Offer>(categoryResult.Error);
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
            saveAsDraft ? OfferStatus.Draft : OfferStatus.Published,
            imageUrl?.Trim(),
            latitude,
            longitude,
            now,
            categoryResult.Value);

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
        DateTime? lastModifiedAtUtc = null,
        int viewCount = 0,
        int version = 1,
        string? category = null,
        int moderationRetryCount = 0,
        DateTime? nextModerationRetryAtUtc = null)
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
            createdAtUtc,
            category)
        {
            LastModifiedAtUtc = lastModifiedAtUtc,
            ViewCount = viewCount,
            Version = version,
            ModerationRetryCount = moderationRetryCount,
            NextModerationRetryAtUtc = nextModerationRetryAtUtc
        };

        return offer;
    }

    /// <summary>
    /// Actualiza los campos editables de la oferta con control de concurrencia optimista y validación de apartados activos (Spec 035, FR-035-01 a FR-035-07).
    /// Devuelve <c>true</c> en caso de éxito si el contenido visible (título, descripción o imagen) cambió y requiere re-moderación.
    /// </summary>
    public Result<bool> Update(
        string title,
        string description,
        decimal originalPrice,
        int discountPercentage,
        int stock,
        DateTime startsAt,
        DateTime endsAt,
        string? imageUrl,
        int activeClaimCount,
        int expectedVersion,
        DateTime nowUtc,
        string? category = null,
        bool requireValidCategory = false)
    {
        if (expectedVersion != Version)
        {
            return Result.Failure<bool>(Error.Conflict("Offer.VersionConflict", "La oferta fue modificada concurrentemente por otra solicitud."));
        }

        if (stock < activeClaimCount)
        {
            return Result.Failure<bool>(Error.Conflict("Offer.StockBelowActiveClaims", $"El stock no puede ser inferior a los {activeClaimCount} apartados activos."));
        }

        if (string.IsNullOrWhiteSpace(title) || title.Trim().Length is < 3 or > 100)
        {
            return Result.Failure<bool>(Error.Validation("Offer.InvalidTitle", "El título de la oferta debe tener entre 3 y 100 caracteres."));
        }

        if (string.IsNullOrWhiteSpace(description) || description.Trim().Length is < 5 or > 500)
        {
            return Result.Failure<bool>(Error.Validation("Offer.InvalidDescription", "La descripción de la oferta debe tener entre 5 y 500 caracteres."));
        }

        if (originalPrice <= 0)
        {
            return Result.Failure<bool>(Error.Validation("Offer.InvalidOriginalPrice", "El precio original debe ser mayor a 0."));
        }

        if (discountPercentage is < 1 or > 90)
        {
            return Result.Failure<bool>(Error.Validation("Offer.InvalidDiscount", "El porcentaje de descuento debe estar entre 1% y 90%."));
        }

        if (stock < 1)
        {
            return Result.Failure<bool>(Error.Validation("Offer.InvalidStock", "El stock de la oferta debe ser de al menos 1 unidad."));
        }

        if (endsAt <= startsAt)
        {
            return Result.Failure<bool>(Error.Validation("Offer.InvalidValidityDates", "La fecha de fin de vigencia debe ser posterior a la fecha de inicio."));
        }

        if (endsAt <= nowUtc)
        {
            return Result.Failure<bool>(Error.Validation("Offer.ExpiredEndDate", "La fecha de vencimiento debe estar en el futuro."));
        }

        Result<string?> categoryResult = ResolveCategoryForUpdate(category, Status, Category, requireValidCategory);
        if (categoryResult.IsFailure)
        {
            return Result.Failure<bool>(categoryResult.Error);
        }

        string trimmedTitle = title.Trim();
        string trimmedDescription = description.Trim();
        string? trimmedImageUrl = imageUrl?.Trim();

        bool contentChanged = !string.Equals(Title, trimmedTitle, StringComparison.Ordinal) ||
                              !string.Equals(Description, trimmedDescription, StringComparison.Ordinal) ||
                              !string.Equals(ImageUrl, trimmedImageUrl, StringComparison.Ordinal);

        Title = trimmedTitle;
        Description = trimmedDescription;
        OriginalPrice = originalPrice;
        DiscountPercentage = discountPercentage;
        DiscountedPrice = Math.Round(originalPrice * (1 - (discountPercentage / 100m)), 2);
        Stock = stock;
        StartsAt = startsAt.ToUniversalTime();
        EndsAt = endsAt.ToUniversalTime();
        ImageUrl = trimmedImageUrl;
        Category = categoryResult.Value;
        Version++;
        LastModifiedAtUtc = nowUtc;

        return Result.Success(contentChanged);
    }

    private static Result<string?> ResolveCategoryForCreate(string? category, bool requireValidCategory)
    {
        if (requireValidCategory)
        {
            if (string.IsNullOrWhiteSpace(category))
            {
                return Result.Failure<string?>(new Error(
                    "Offer.CategoryRequired",
                    "La categoría de la oferta es obligatoria.",
                    ErrorType.Validation,
                    new Dictionary<string, object?> { ["field"] = "category" }));
            }

            if (!OfferCategoryCatalog.TryParse(category, out OfferCategory parsed))
            {
                return Result.Failure<string?>(new Error(
                    "Offer.InvalidCategory",
                    $"La categoría debe pertenecer al catálogo OfferCategory: {string.Join(", ", OfferCategoryCatalog.Names)}.",
                    ErrorType.Validation,
                    new Dictionary<string, object?> { ["field"] = "category" }));
            }

            return Result.Success<string?>(parsed.ToString());
        }

        if (string.IsNullOrWhiteSpace(category))
        {
            return Result.Success<string?>(null);
        }

        return Result.Success<string?>(OfferCategoryCatalog.TryParse(category, out OfferCategory canonical)
            ? canonical.ToString()
            : category.Trim());
    }

    private static Result<string?> ResolveCategoryForUpdate(
        string? category,
        OfferStatus status,
        string? currentCategory,
        bool requireValidCategory)
    {
        if (requireValidCategory)
        {
            if (string.IsNullOrWhiteSpace(category))
            {
                if (status == OfferStatus.Published || !string.IsNullOrWhiteSpace(currentCategory))
                {
                    return Result.Failure<string?>(new Error(
                        "Offer.CategoryRequired",
                        "Una oferta publicada debe conservar una categoría válida de OfferCategory.",
                        ErrorType.Validation,
                        new Dictionary<string, object?> { ["field"] = "category" }));
                }

                return Result.Success<string?>(currentCategory);
            }

            if (!OfferCategoryCatalog.TryParse(category, out OfferCategory parsed))
            {
                return Result.Failure<string?>(new Error(
                    "Offer.InvalidCategory",
                    $"La categoría debe pertenecer al catálogo OfferCategory: {string.Join(", ", OfferCategoryCatalog.Names)}.",
                    ErrorType.Validation,
                    new Dictionary<string, object?> { ["field"] = "category" }));
            }

            return Result.Success<string?>(parsed.ToString());
        }

        if (string.IsNullOrWhiteSpace(category))
        {
            return Result.Success<string?>(currentCategory);
        }

        return Result.Success<string?>(OfferCategoryCatalog.TryParse(category, out OfferCategory canonical)
            ? canonical.ToString()
            : category.Trim());
    }

    /// <summary>
    /// Control automático de vigencia: marca la oferta como expirada si se alcanzó la fecha y hora de vencimiento (SE-022 a SE-025).
    /// </summary>
    public Result Expire(DateTime nowUtc)
    {
        if (Status is OfferStatus.Draft or OfferStatus.PendingModeration or OfferStatus.Rejected)
        {
            return Result.Failure(Error.Conflict("Offer.DraftRequiresModeration", "La oferta debe superar la moderación antes de cambiar de estado."));
        }

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
        if (Status is OfferStatus.Draft or OfferStatus.PendingModeration)
        {
            return Result.Failure(Error.Conflict("Offer.DraftRequiresModeration", "La oferta debe moderarse antes de salir de ese estado."));
        }

        Status = OfferStatus.Archived;
        LastModifiedAtUtc = nowUtc;
        return Result.Success();
    }

    /// <summary>
    /// Coloca una oferta recién creada en espera de re-moderación diferida (Spec 034, FR-034-07).
    /// </summary>
    public Result MarkPendingModeration(DateTime nowUtc, DateTime nextRetryAtUtc)
    {
        if (Status != OfferStatus.Published)
        {
            return Result.Failure(Error.Conflict("Offer.InvalidTransition", "Solo una oferta pendiente de publicación inicial puede pasar a PendingModeration."));
        }

        Status = OfferStatus.PendingModeration;
        ModerationRetryCount = 0;
        NextModerationRetryAtUtc = DateTime.SpecifyKind(nextRetryAtUtc, DateTimeKind.Utc);
        LastModifiedAtUtc = DateTime.SpecifyKind(nowUtc, DateTimeKind.Utc);
        return Result.Success();
    }

    /// <summary>
    /// Publica o expira una oferta pendiente tras aprobarse en re-moderación (Spec 034, FR-034-07).
    /// </summary>
    public Result ApproveFromModeration(DateTime nowUtc)
    {
        if (Status != OfferStatus.PendingModeration)
        {
            return Result.Failure(Error.Conflict("Offer.InvalidTransition", "Solo una oferta en PendingModeration puede aprobarse."));
        }

        DateTime normalizedNow = DateTime.SpecifyKind(nowUtc, DateTimeKind.Utc);
        Status = EndsAt <= normalizedNow ? OfferStatus.Expired : OfferStatus.Published;
        NextModerationRetryAtUtc = null;
        LastModifiedAtUtc = normalizedNow;
        return Result.Success();
    }

    /// <summary>
    /// Rechaza definitivamente una oferta en PendingModeration (Spec 034, FR-034-07).
    /// </summary>
    public Result RejectFromModeration(DateTime nowUtc)
    {
        if (Status != OfferStatus.PendingModeration)
        {
            return Result.Failure(Error.Conflict("Offer.InvalidTransition", "Solo una oferta en PendingModeration puede rechazarse."));
        }

        Status = OfferStatus.Rejected;
        NextModerationRetryAtUtc = null;
        LastModifiedAtUtc = DateTime.SpecifyKind(nowUtc, DateTimeKind.Utc);
        return Result.Success();
    }

    /// <summary>
    /// Registra un intento fallido de re-moderación; al agotar el máximo, rechaza fail-closed (Spec 034, FR-034-07).
    /// </summary>
    public Result ScheduleModerationRetry(DateTime nowUtc, DateTime nextRetryAtUtc, int maxAttempts)
    {
        if (Status != OfferStatus.PendingModeration)
        {
            return Result.Failure(Error.Conflict("Offer.InvalidTransition", "Solo una oferta en PendingModeration admite reintento de moderación."));
        }

        DateTime normalizedNow = DateTime.SpecifyKind(nowUtc, DateTimeKind.Utc);
        ModerationRetryCount++;
        LastModifiedAtUtc = normalizedNow;

        if (ModerationRetryCount >= Math.Max(1, maxAttempts))
        {
            Status = OfferStatus.Rejected;
            NextModerationRetryAtUtc = null;
        }
        else
        {
            NextModerationRetryAtUtc = DateTime.SpecifyKind(nextRetryAtUtc, DateTimeKind.Utc);
        }

        return Result.Success();
    }

    /// <summary>
    /// Indica si la oferta se encuentra vigente a una fecha dada.
    /// </summary>
    public bool IsActive(DateTime nowUtc) =>
        Status == OfferStatus.Published && nowUtc >= StartsAt && nowUtc < EndsAt && Stock > 0;

    /// <summary>
    /// Devuelve unidades al inventario cuando un apartado expira o no puede completarse
    /// (FR-033-05, NFR-033-02).
    /// </summary>
    public Result ReturnStock(int units)
    {
        if (units <= 0)
        {
            return Result.Failure(
                Error.Validation("Offer.InvalidReturnUnits", "Las unidades a devolver deben ser mayores a 0."));
        }

        Stock += units;
        return Result.Success();
    }

    /// <summary>
    /// Incrementa el contador de vistas cuando la deduplicación diaria confirma una vista nueva
    /// (FR-032-03, CA-032-03 y CA-032-04).
    /// </summary>
    public Result RegisterView()
    {
        if (Status == OfferStatus.Archived)
        {
            return Result.Failure(
                Error.Conflict("Offer.ArchivedNotViewable", "No se registran vistas sobre ofertas archivadas."));
        }

        ViewCount++;
        return Result.Success();
    }
}
