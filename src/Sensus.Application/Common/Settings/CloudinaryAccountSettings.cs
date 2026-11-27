namespace Sensus.Application.Common.Settings;

/// <summary>
/// Configuración pública de cuenta Cloudinary consumida por validadores de Application sin acoplarse a Infrastructure (FR-003-06).
/// </summary>
public sealed class CloudinaryAccountSettings
{
    public const string SectionName = "Cloudinary";

    public string CloudName { get; set; } = string.Empty;
}
