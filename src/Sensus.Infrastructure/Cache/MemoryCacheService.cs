using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sensus.Application.Common.Interfaces;

namespace Sensus.Infrastructure.Cache;

/// <summary>
/// Configuración de la caché en memoria de proceso (Spec 048, sección <c>Cache:Memory</c>).
/// </summary>
public sealed class MemoryCacheSettings
{
    public const string SectionName = "Cache:Memory";

    /// <summary>Activa la caché en memoria cuando Redis no está habilitado.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Máximo de entradas por instancia; al superarlo se purgan las que vencen primero.</summary>
    public int MaxEntries { get; set; } = 1000;

    /// <summary>
    /// Tope del TTL. Como cada instancia de Lambda tiene su propia caché y la invalidación es local, este tope
    /// acota la ventana máxima de datos desactualizados entre instancias.
    /// </summary>
    public int MaxTtlSeconds { get; set; } = 60;
}

/// <summary>
/// Caché de lecturas en memoria de proceso, sin costo ni infraestructura (Spec 048, alternativa documentada a
/// ElastiCache dentro del nivel gratuito). Se usa solo cuando Redis está apagado. Es por instancia: no se comparte
/// entre Lambdas, por eso el TTL está acotado. Guarda JSON inmutable (nunca referencias vivas) y no admite
/// contadores atómicos, que viven en DynamoDB (Spec 049).
/// </summary>
public sealed class MemoryCacheService : ICacheService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly ConcurrentDictionary<string, Entry> _entries = new(StringComparer.Ordinal);
    private readonly MemoryCacheSettings _settings;
    private readonly RedisSettings _ttlSettings;
    private readonly IDateTimeProvider _clock;
    private readonly ILogger<MemoryCacheService> _logger;

    public MemoryCacheService(
        IOptions<MemoryCacheSettings> settings,
        IOptions<RedisSettings> ttlSettings,
        IDateTimeProvider clock,
        ILogger<MemoryCacheService> logger)
    {
        _settings = settings.Value;
        _ttlSettings = ttlSettings.Value;
        _clock = clock;
        _logger = logger;
    }

    public bool IsAvailable => _settings.Enabled;

    public string Provider => _settings.Enabled ? "memory" : "none";

    /// <summary>Número de entradas almacenadas (incluye las vencidas aún no purgadas). Solo para pruebas y métricas.</summary>
    public int Count => _entries.Count;

    public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        if (!_settings.Enabled || string.IsNullOrWhiteSpace(key) || !_entries.TryGetValue(key, out Entry? entry))
        {
            return Task.FromResult<T?>(default);
        }

        if (entry.ExpiresAtUtc <= _clock.UtcNow)
        {
            _entries.TryRemove(new KeyValuePair<string, Entry>(key, entry));
            return Task.FromResult<T?>(default);
        }

        try
        {
            return Task.FromResult(JsonSerializer.Deserialize<T>(entry.Json, JsonOptions));
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Entrada de caché en memoria ilegible para '{Key}'; se descarta.", key);
            _entries.TryRemove(key, out _);
            return Task.FromResult<T?>(default);
        }
    }

    public Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken cancellationToken = default)
    {
        if (!_settings.Enabled || string.IsNullOrWhiteSpace(key) || value is null)
        {
            return Task.CompletedTask;
        }

        TimeSpan requested = expiration ?? TimeSpan.FromMinutes(_ttlSettings.DefaultTtlMinutes > 0 ? _ttlSettings.DefaultTtlMinutes : 30);
        TimeSpan ttl = Min(requested, TimeSpan.FromSeconds(Math.Max(1, _settings.MaxTtlSeconds)));
        if (ttl <= TimeSpan.Zero)
        {
            return Task.CompletedTask;
        }

        DateTime now = _clock.UtcNow;
        MakeRoom(key, now);
        _entries[key] = new Entry(JsonSerializer.Serialize(value, JsonOptions), now.Add(ttl));
        return Task.CompletedTask;
    }

    public Task SetForEntityAsync<T>(string key, T value, string entityName, CancellationToken cancellationToken = default) =>
        SetAsync(key, value, _ttlSettings.GetTtlForEntity(entityName), cancellationToken);

    public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(key))
        {
            _entries.TryRemove(key, out _);
        }

        return Task.CompletedTask;
    }

    public Task RemoveByPrefixAsync(string prefix, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(prefix))
        {
            return Task.CompletedTask;
        }

        foreach (string key in _entries.Keys.Where(k => k.StartsWith(prefix, StringComparison.Ordinal)))
        {
            _entries.TryRemove(key, out _);
        }

        return Task.CompletedTask;
    }

    public async Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default) =>
        await GetAsync<JsonElement?>(key, cancellationToken) is not null;

    public Task<double?> IncrementByFloatAsync(string key, double value, TimeSpan? expiration = null, CancellationToken cancellationToken = default) =>
        Task.FromResult<double?>(null);

    public Task<long?> IncrementAsync(string key, long value = 1, TimeSpan? expiration = null, CancellationToken cancellationToken = default) =>
        Task.FromResult<long?>(null);

    private void MakeRoom(string incomingKey, DateTime now)
    {
        int max = Math.Max(1, _settings.MaxEntries);
        if (_entries.Count < max || _entries.ContainsKey(incomingKey))
        {
            return;
        }

        foreach (KeyValuePair<string, Entry> expired in _entries.Where(e => e.Value.ExpiresAtUtc <= now).ToList())
        {
            _entries.TryRemove(expired);
        }

        if (_entries.Count < max)
        {
            return;
        }

        // Sigue lleno: se expulsa el 10 % de las entradas que vencen antes.
        int toEvict = Math.Max(1, max / 10);
        foreach (KeyValuePair<string, Entry> oldest in _entries.OrderBy(e => e.Value.ExpiresAtUtc).Take(toEvict).ToList())
        {
            _entries.TryRemove(oldest);
        }
    }

    private static TimeSpan Min(TimeSpan a, TimeSpan b) => a <= b ? a : b;

    private sealed record Entry(string Json, DateTime ExpiresAtUtc);
}
