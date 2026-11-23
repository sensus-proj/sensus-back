using Amazon;

namespace Sensus.Infrastructure.Services;

/// <summary>
/// Configuración del transporte transaccional de correo mediante Amazon SES (Spec 029, FR-029-04 y FR-029-12).
/// El remitente y la región se resuelven desde SSM Parameter Store en producción; nunca desde el repositorio.
/// </summary>
public sealed class SesEmailSettings
{
    public const string SectionName = "SesEmailSettings";

    /// <summary>Remitente verificado en SES. Obligatorio cuando <see cref="Simulate"/> es false.</summary>
    public string From { get; set; } = string.Empty;

    /// <summary>Región de AWS. Vacía para usar la resolución predeterminada del SDK.</summary>
    public string Region { get; set; } = string.Empty;

    /// <summary>Configuration Set opcional de SES para métricas y reputación.</summary>
    public string ConfigurationSetName { get; set; } = string.Empty;

    /// <summary>Solo para Development/Testing: registra el envío redactado sin contactar a AWS.</summary>
    public bool Simulate { get; set; }

    public RegionEndpoint? GetRegionEndpoint() =>
        string.IsNullOrWhiteSpace(Region) ? null : RegionEndpoint.GetBySystemName(Region.Trim());
}
