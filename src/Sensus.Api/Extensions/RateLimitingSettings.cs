namespace Sensus.Api.Extensions;

public sealed class RateLimitingSettings
{
    public const string SectionName = "RateLimiting";

    public RateLimitTierSettings AuthStrict { get; set; } = new() { PermitLimit = 10, WindowSeconds = 60 };
    public RateLimitTierSettings Recover { get; set; } = new() { PermitLimit = 3, WindowSeconds = 3600 };
    public RateLimitTierSettings ReadHeavy { get; set; } = new() { PermitLimit = 120, WindowSeconds = 60 };
    public RateLimitTierSettings SensitiveWrite { get; set; } = new() { PermitLimit = 30, WindowSeconds = 60 };
    public RateLimitTierSettings Favorites { get; set; } = new() { PermitLimit = 120, WindowSeconds = 3600 };
    public RateLimitTierSettings AiChat { get; set; } = new() { PermitLimit = 10, WindowSeconds = 60 };
    public RateLimitTierSettings PostLikes { get; set; } = new() { PermitLimit = 60, WindowSeconds = 3600 };
}

public sealed class RateLimitTierSettings
{
    public int PermitLimit { get; set; } = 30;
    public int WindowSeconds { get; set; } = 60;
}
