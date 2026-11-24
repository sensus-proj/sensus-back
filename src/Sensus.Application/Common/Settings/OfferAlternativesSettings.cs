using Sensus.Domain.Models;

namespace Sensus.Application.Common.Settings;

/// <summary>
/// Configuración externalizada de radio, límite y proporción de precio comparable para alternativas (Spec 043).
/// </summary>
public sealed class OfferAlternativesSettings
{
    public const string SectionName = "OfferAlternatives";

    public int DefaultLimit { get; set; } = OfferAlternativesPolicy.DefaultLimit;
    public int MaxLimit { get; set; } = OfferAlternativesPolicy.MaxLimit;
    public int RegisteredMaxDistanceMeters { get; set; } = OfferAlternativesPolicy.RegisteredMaxDistanceMeters;
    public int GuestMaxDistanceMeters { get; set; } = OfferAlternativesPolicy.GuestMaxDistanceMeters;
    public decimal ComparablePriceRatio { get; set; } = OfferAlternativesPolicy.DefaultComparablePriceRatio;
}
