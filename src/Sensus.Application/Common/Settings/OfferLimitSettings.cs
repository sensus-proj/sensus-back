using System.ComponentModel.DataAnnotations;

namespace Sensus.Application.Common.Settings;

/// <summary>
/// Configuración del límite de ofertas activas simultáneas por comercio (Spec 034, FR-034-05).
/// </summary>
public sealed class OfferLimitSettings
{
    public const string SectionName = "Offers";
    public const int DefaultMaxActivePerMerchant = 20;

    /// <summary>
    /// Máximo de ofertas publicadas y vigentes (<c>EndsAt &gt; nowUtc</c>) por comercio.
    /// </summary>
    [Range(1, 1000)]
    public int MaxActivePerMerchant { get; set; } = DefaultMaxActivePerMerchant;
}
