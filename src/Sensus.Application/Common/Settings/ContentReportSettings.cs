namespace Sensus.Application.Common.Settings;

/// <summary>
/// Configuración externalizada para reportes de contenido comunitario (Spec 040).
/// </summary>
public sealed class ContentReportSettings
{
    public const string SectionName = "ContentReports";

    public int AutoReviewThreshold { get; set; } = 3;
    public int DeduplicationWindowHours { get; set; } = 24;
}
