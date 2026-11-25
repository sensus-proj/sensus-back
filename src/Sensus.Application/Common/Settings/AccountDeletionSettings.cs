namespace Sensus.Application.Common.Settings;

/// <summary>
/// Configuración de baja de cuenta, purga diferida y retención de auditoría anonimizada
/// (Specs 060 y 061, sección <c>AccountDeletion</c>). Los valores viven en configuración, nunca en código.
/// </summary>
public sealed class AccountDeletionSettings
{
    public const string SectionName = "AccountDeletion";

    public int RetentionDays { get; set; } = 30;

    public int PurgeIntervalHours { get; set; } = 24;

    public int ModerationAuditRetentionDays { get; set; } = 90;
}
