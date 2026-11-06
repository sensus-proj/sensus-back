namespace Sensus.Infrastructure.Services;

public sealed class OpenAiSettings
{
    public const string SectionName = "OpenAiSettings";

    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = "omni-moderation-latest";
    public string BaseUrl { get; set; } = "https://api.openai.com/v1";
    public int TimeoutSeconds { get; set; } = 15;
}
