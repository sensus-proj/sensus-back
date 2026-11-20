using Sensus.Domain.Common;
using Sensus.Domain.Enums;

namespace Sensus.Domain.Entities;

/// <summary>
/// Elemento individual dentro de un documento de interacción (una página de feed o un evento puntual).
/// Nunca contiene coordenadas exactas ni datos personales (Spec 055, FR-055-05, NFR-055-02).
/// </summary>
public sealed record OfferInteractionItem
{
    public const string UncategorizedCategory = "Uncategorized";
    public const int MaxCategoryLength = 32;

    public Guid OfferId { get; }
    public int Position { get; }
    public FeedSlot Slot { get; }
    public string Category { get; }
    public DistanceBucket DistanceBucket { get; }

    private OfferInteractionItem(
        Guid offerId,
        int position,
        FeedSlot slot,
        string category,
        DistanceBucket distanceBucket)
    {
        OfferId = offerId;
        Position = position;
        Slot = slot;
        Category = category;
        DistanceBucket = distanceBucket;
    }

    public static Result<OfferInteractionItem> Create(
        Guid offerId,
        int position,
        FeedSlot slot,
        string? rawCategory,
        DistanceBucket distanceBucket)
    {
        if (offerId == Guid.Empty)
        {
            return Result.Failure<OfferInteractionItem>(
                Error.Validation("OfferInteraction.OfferRequired", "El identificador de la oferta es obligatorio."));
        }

        if (position < 1)
        {
            return Result.Failure<OfferInteractionItem>(
                Error.Validation("OfferInteraction.InvalidPosition", "La posición de la oferta debe ser mayor o igual a 1."));
        }

        if (!Enum.IsDefined(slot))
        {
            return Result.Failure<OfferInteractionItem>(
                Error.Validation("OfferInteraction.InvalidSlot", "El slot de la interacción no es válido."));
        }

        if (!Enum.IsDefined(distanceBucket))
        {
            return Result.Failure<OfferInteractionItem>(
                Error.Validation("OfferInteraction.InvalidDistanceBucket", "El rango de distancia no es válido."));
        }

        string normalizedCategory = ResolveCategory(rawCategory);
        return Result.Success(new OfferInteractionItem(offerId, position, slot, normalizedCategory, distanceBucket));
    }

    public static OfferInteractionItem Reconstitute(
        Guid offerId,
        int position,
        FeedSlot slot,
        string category,
        DistanceBucket distanceBucket) =>
        new(offerId, position, slot, category, distanceBucket);

    private static string ResolveCategory(string? rawCategory)
    {
        if (string.IsNullOrWhiteSpace(rawCategory))
        {
            return UncategorizedCategory;
        }

        if (OfferCategoryCatalog.TryNormalizeLegacy(rawCategory, out OfferCategory parsed))
        {
            return parsed.ToString();
        }

        string trimmed = rawCategory.Trim();
        return trimmed.Length <= MaxCategoryLength ? trimmed : trimmed[..MaxCategoryLength];
    }
}

/// <summary>
/// Documento de interacción seudonimizada del usuario registrado (Spec 055).
/// Agrupa las impresiones de una página del feed en un solo documento para acotar el almacenamiento en Atlas M0.
/// </summary>
public sealed class OfferInteraction : BaseEntity
{
    public const int ExpectedPseudoUserIdLength = 64;
    public const int MaxItemsPerBatch = 100;
    public const int MaxRankingVersionLength = 32;
    public const int MaxSnapshotIdLength = 64;

    private readonly List<OfferInteractionItem> _items = [];

    public OfferInteractionType Type { get; private set; }
    public string PseudoUserId { get; private set; } = string.Empty;
    public int KeyVersion { get; private set; }
    public string? SnapshotId { get; private set; }
    public string RankingVersion { get; private set; } = string.Empty;
    public int Page { get; private set; }
    public IReadOnlyList<OfferInteractionItem> Items => _items.AsReadOnly();
    public DateTime OccurredAtUtc { get; private set; }

    private OfferInteraction() : base()
    {
    }

    private OfferInteraction(
        Guid id,
        OfferInteractionType type,
        string pseudoUserId,
        int keyVersion,
        string? snapshotId,
        string rankingVersion,
        int page,
        IEnumerable<OfferInteractionItem> items,
        DateTime occurredAtUtc)
        : base(id)
    {
        Type = type;
        PseudoUserId = pseudoUserId;
        KeyVersion = keyVersion;
        SnapshotId = snapshotId;
        RankingVersion = rankingVersion;
        Page = page;
        _items.AddRange(items);
        OccurredAtUtc = occurredAtUtc;
    }

    public static Result<OfferInteraction> Create(
        OfferInteractionType type,
        string pseudoUserId,
        int keyVersion,
        string? snapshotId,
        string rankingVersion,
        int page,
        IReadOnlyList<OfferInteractionItem>? items,
        DateTime occurredAtUtc)
    {
        if (!Enum.IsDefined(type))
        {
            return Result.Failure<OfferInteraction>(
                Error.Validation("OfferInteraction.InvalidType", "El tipo de interacción no es válido."));
        }

        if (!IsValidHexDigest(pseudoUserId))
        {
            return Result.Failure<OfferInteraction>(
                Error.Validation("OfferInteraction.InvalidPseudonym", "El seudónimo HMAC debe ser un hash hexadecimal de 64 caracteres."));
        }

        if (keyVersion < 1)
        {
            return Result.Failure<OfferInteraction>(
                Error.Validation("OfferInteraction.InvalidKeyVersion", "La versión de clave HMAC debe ser mayor o igual a 1."));
        }

        if (string.IsNullOrWhiteSpace(rankingVersion) || rankingVersion.Trim().Length > MaxRankingVersionLength)
        {
            return Result.Failure<OfferInteraction>(
                Error.Validation("OfferInteraction.InvalidRankingVersion", "La versión de ranking es obligatoria y acotada."));
        }

        string? normalizedSnapshot = string.IsNullOrWhiteSpace(snapshotId) ? null : snapshotId.Trim();
        if (normalizedSnapshot is not null && normalizedSnapshot.Length > MaxSnapshotIdLength)
        {
            return Result.Failure<OfferInteraction>(
                Error.Validation("OfferInteraction.InvalidSnapshotId", "El identificador de instantánea excede el máximo permitido."));
        }

        if (page < 1)
        {
            return Result.Failure<OfferInteraction>(
                Error.Validation("OfferInteraction.InvalidPage", "La página debe ser mayor o igual a 1."));
        }

        if (items is null || items.Count == 0)
        {
            return Result.Failure<OfferInteraction>(
                Error.Validation("OfferInteraction.ItemsRequired", "La interacción debe incluir al menos una oferta."));
        }

        if (type == OfferInteractionType.Impression && items.Count > MaxItemsPerBatch)
        {
            return Result.Failure<OfferInteraction>(
                Error.Validation("OfferInteraction.TooManyItems", "Una página de impresiones no puede superar 100 elementos."));
        }

        if (type != OfferInteractionType.Impression && items.Count != 1)
        {
            return Result.Failure<OfferInteraction>(
                Error.Validation("OfferInteraction.SingleItemRequired", "Los eventos open y claim deben contener exactamente un elemento."));
        }

        return Result.Success(new OfferInteraction(
            Guid.NewGuid(),
            type,
            pseudoUserId.ToLowerInvariant(),
            keyVersion,
            normalizedSnapshot,
            rankingVersion.Trim(),
            page,
            items,
            occurredAtUtc.ToUniversalTime()));
    }

    public static OfferInteraction Reconstitute(
        Guid id,
        OfferInteractionType type,
        string pseudoUserId,
        int keyVersion,
        string? snapshotId,
        string rankingVersion,
        int page,
        IEnumerable<OfferInteractionItem> items,
        DateTime occurredAtUtc) =>
        new(id, type, pseudoUserId, keyVersion, snapshotId, rankingVersion, page, items, occurredAtUtc);

    private static bool IsValidHexDigest(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length != ExpectedPseudoUserIdLength)
        {
            return false;
        }

        foreach (char c in value)
        {
            bool isHex = c is (>= '0' and <= '9') or (>= 'a' and <= 'f') or (>= 'A' and <= 'F');
            if (!isHex)
            {
                return false;
            }
        }

        return true;
    }
}
