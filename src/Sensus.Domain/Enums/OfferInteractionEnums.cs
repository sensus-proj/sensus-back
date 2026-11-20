using System.Globalization;

namespace Sensus.Domain.Enums;

/// <summary>
/// Tipo de interacción registrada del usuario con una oferta (Spec 055, FR-055-01 a FR-055-03).
/// </summary>
public enum OfferInteractionType
{
    Impression = 1,
    Open = 2,
    Claim = 3
}

/// <summary>
/// Espacio o mecanismo mediante el cual se sirvió la oferta al usuario (Spec 055, FR-055-01).
/// </summary>
public enum FeedSlot
{
    Ranked = 1,
    Exploration = 2,
    Fallback = 3,
    Direct = 4
}

/// <summary>
/// Rango discretizado de distancia para evitar almacenar coordenadas exactas (Spec 055, FR-055-05).
/// </summary>
public enum DistanceBucket
{
    Range0To1Km = 1,
    Range1To3Km = 2,
    Range3To10Km = 3,
    Range10PlusKm = 4,
    Unknown = 5
}

/// <summary>
/// Utilidades deterministas para convertir tipos, slots y distancias al formato canónico sin PII (Spec 055).
/// </summary>
public static class OfferInteractionCatalog
{
    public static string ToWireName(this OfferInteractionType type) => type switch
    {
        OfferInteractionType.Impression => "impression",
        OfferInteractionType.Open => "open",
        OfferInteractionType.Claim => "claim",
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Tipo de interacción no soportado.")
    };

    public static string ToWireName(this FeedSlot slot) => slot switch
    {
        FeedSlot.Ranked => "ranked",
        FeedSlot.Exploration => "exploration",
        FeedSlot.Fallback => "fallback",
        FeedSlot.Direct => "direct",
        _ => throw new ArgumentOutOfRangeException(nameof(slot), slot, "Slot de feed no soportado.")
    };

    public static string ToWireName(this DistanceBucket bucket) => bucket switch
    {
        DistanceBucket.Range0To1Km => "0-1",
        DistanceBucket.Range1To3Km => "1-3",
        DistanceBucket.Range3To10Km => "3-10",
        DistanceBucket.Range10PlusKm => "10+",
        DistanceBucket.Unknown => "unknown",
        _ => throw new ArgumentOutOfRangeException(nameof(bucket), bucket, "Rango de distancia no soportado.")
    };

    public static OfferInteractionType ParseType(string raw) => raw?.Trim().ToLower(CultureInfo.InvariantCulture) switch
    {
        "impression" => OfferInteractionType.Impression,
        "open" => OfferInteractionType.Open,
        "claim" => OfferInteractionType.Claim,
        _ => throw new ArgumentException("Tipo de interacción inválido.", nameof(raw))
    };

    public static FeedSlot ParseSlot(string raw) => raw?.Trim().ToLower(CultureInfo.InvariantCulture) switch
    {
        "ranked" => FeedSlot.Ranked,
        "exploration" => FeedSlot.Exploration,
        "fallback" => FeedSlot.Fallback,
        "direct" => FeedSlot.Direct,
        _ => throw new ArgumentException("Slot de interacción inválido.", nameof(raw))
    };

    public static DistanceBucket ParseDistanceBucket(string raw) => raw?.Trim().ToLower(CultureInfo.InvariantCulture) switch
    {
        "0-1" => DistanceBucket.Range0To1Km,
        "1-3" => DistanceBucket.Range1To3Km,
        "3-10" => DistanceBucket.Range3To10Km,
        "10+" => DistanceBucket.Range10PlusKm,
        "unknown" => DistanceBucket.Unknown,
        _ => throw new ArgumentException("Rango de distancia inválido.", nameof(raw))
    };

    /// <summary>
    /// Clasifica una distancia en kilómetros en rangos fijos sin conservar coordenadas exactas (FR-055-05).
    /// </summary>
    public static DistanceBucket ClassifyDistance(double? distanceKm)
    {
        if (!distanceKm.HasValue || double.IsNaN(distanceKm.Value) || double.IsInfinity(distanceKm.Value) || distanceKm.Value < 0d)
        {
            return DistanceBucket.Unknown;
        }

        double km = distanceKm.Value;
        if (km < 1d)
        {
            return DistanceBucket.Range0To1Km;
        }

        if (km < 3d)
        {
            return DistanceBucket.Range1To3Km;
        }

        return km <= 10d
            ? DistanceBucket.Range3To10Km
            : DistanceBucket.Range10PlusKm;
    }
}
