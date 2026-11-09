namespace Sensus.Infrastructure.Cache;

public sealed class RedisSettings
{
    public const string SectionName = "Redis";

    public string ConnectionString { get; set; } = string.Empty;

    public bool Enabled { get; set; } = true;

    public int DefaultTtlMinutes { get; set; } = 30;

    public Dictionary<string, int> TtlByEntityMinutes { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public TimeSpan GetTtlForEntity(string entityName)
    {
        if (TtlByEntityMinutes.TryGetValue(entityName, out var minutes) && minutes > 0)
        {
            return TimeSpan.FromMinutes(minutes);
        }

        return TimeSpan.FromMinutes(DefaultTtlMinutes > 0 ? DefaultTtlMinutes : 30);
    }
}
