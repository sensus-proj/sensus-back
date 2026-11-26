using System.ComponentModel.DataAnnotations;

namespace Sensus.Application.Common.Settings;

/// <summary>
/// Configuración de reintentos de moderación diferida para ofertas en <c>PendingModeration</c>
/// (Spec 034, FR-034-07).
/// </summary>
public sealed class OfferModerationRetrySettings
{
    public const string SectionName = "Moderation:OfferRetry";
    public const int DefaultMaxAttempts = 5;
    public const int DefaultRetryDelaySeconds = 60;
    public const int DefaultCheckIntervalSeconds = 60;
    public const int DefaultBatchSize = 20;

    [Range(1, 50)]
    public int MaxAttempts { get; set; } = DefaultMaxAttempts;

    [Range(5, 3600)]
    public int RetryDelaySeconds { get; set; } = DefaultRetryDelaySeconds;

    [Range(5, 3600)]
    public int CheckIntervalSeconds { get; set; } = DefaultCheckIntervalSeconds;

    [Range(1, 100)]
    public int BatchSize { get; set; } = DefaultBatchSize;
}
