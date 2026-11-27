using System.ComponentModel.DataAnnotations;
using Sensus.Application.Common.Settings;

namespace Sensus.Infrastructure.Services;

public sealed class CategoryThreshold
{
    public double Low { get; set; } = 0.35;
    public double High { get; set; } = 0.75;
}

public sealed class ModerationRetrySettings
{
    public int MaxRetries { get; set; } = 1;
    public int RetryDelayMilliseconds { get; set; } = 500;
}

public sealed class OpenAiSettings
{
    public const string SectionName = "OpenAiSettings";

    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = "omni-moderation-latest";
    public string BaseUrl { get; set; } = "https://api.openai.com/v1";
    public int TimeoutSeconds { get; set; } = 8;

    public double DefaultLowThreshold { get; set; } = 0.35;
    public double DefaultHighThreshold { get; set; } = 0.75;

    public Dictionary<string, CategoryThreshold> CategoryThresholds { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public ModerationRetrySettings Moderation { get; set; } = new();

    public SensiSettings Sensi { get; set; } = new();
}
