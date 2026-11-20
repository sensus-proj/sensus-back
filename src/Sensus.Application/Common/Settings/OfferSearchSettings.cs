namespace Sensus.Application.Common.Settings;

/// <summary>
/// Configuración externalizada de pesos y límites de la búsqueda de ofertas (Spec 041, FR-041-07).
/// </summary>
public sealed class OfferSearchSettings
{
    public const string SectionName = "OfferSearch";

    public string Version { get; set; } = "search-v1";
    public double DefaultRadiusKm { get; set; } = 10d;
    public double MaxRadiusKm { get; set; } = 50d;
    public double GuestMaxRadiusKm { get; set; } = 2d;
    public int MaxCandidates { get; set; } = 200;
    public OfferSearchWeightSettings Weights { get; set; } = new();
}

public sealed class OfferSearchWeightSettings
{
    public double Text { get; set; } = 0.45d;
    public double CategoryMatch { get; set; } = 0.25d;
    public double Proximity { get; set; } = 0.20d;
    public double Popularity { get; set; } = 0.10d;
}
