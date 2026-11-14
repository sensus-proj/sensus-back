using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sensus.Application.Common.Interfaces;
using StackExchange.Redis;

namespace Sensus.Infrastructure.Cache;

/// <summary>
/// Implementación de ICacheService basada en StackExchange.Redis con tolerancia a fallos (fail-graceful).
/// Cumple con FR-048-01, FR-048-02 y FR-048-11.
/// </summary>
public sealed class RedisCacheService : ICacheService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IConnectionMultiplexer? _connection;
    private readonly RedisSettings _settings;
    private readonly ILogger<RedisCacheService> _logger;

    public RedisCacheService(
        IOptions<RedisSettings> settings,
        ILogger<RedisCacheService> logger,
        IConnectionMultiplexer? connection = null)
    {
        _settings = settings.Value;
        _logger = logger;
        _connection = connection;
    }

    private IDatabase? GetDatabase()
    {
        if (!_settings.Enabled || _connection is null || !_connection.IsConnected)
        {
            return null;
        }

        try
        {
            return _connection.GetDatabase();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error al obtener la base de datos de Redis; operando en modo fallback a BD.");
            return null;
        }
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return default;
        }

        var db = GetDatabase();
        if (db is null)
        {
            return default;
        }

        try
        {
            var value = await db.StringGetAsync(key);
            if (!value.HasValue)
            {
                return default;
            }

            return JsonSerializer.Deserialize<T>((string)value!, JsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error al consultar la clave '{Key}' en Redis; fallback a persistencia.", key);
            return default;
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key) || value is null)
        {
            return;
        }

        var db = GetDatabase();
        if (db is null)
        {
            return;
        }

        try
        {
            var ttl = expiration ?? TimeSpan.FromMinutes(_settings.DefaultTtlMinutes > 0 ? _settings.DefaultTtlMinutes : 30);
            var serialized = JsonSerializer.Serialize(value, JsonOptions);
            await db.StringSetAsync(key, serialized, ttl);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error al guardar la clave '{Key}' en Redis; operación omitida de forma transparente.", key);
        }
    }

    public Task SetForEntityAsync<T>(string key, T value, string entityName, CancellationToken cancellationToken = default)
    {
        return SetAsync(key, value, _settings.GetTtlForEntity(entityName), cancellationToken);
    }

    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        var db = GetDatabase();
        if (db is null)
        {
            return;
        }

        try
        {
            await db.KeyDeleteAsync(key);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error al invalidar la clave '{Key}' en Redis.", key);
        }
    }

    public async Task RemoveByPrefixAsync(string prefix, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(prefix) || _connection is null || !_connection.IsConnected)
        {
            return;
        }

        try
        {
            var endpoints = _connection.GetEndPoints();
            foreach (var endpoint in endpoints)
            {
                var server = _connection.GetServer(endpoint);
                if (!server.IsConnected) continue;

                var keys = new List<RedisKey>();
                await foreach (var key in server.KeysAsync(pattern: $"{prefix}*"))
                {
                    keys.Add(key);
                }

                if (keys.Count > 0)
                {
                    var db = _connection.GetDatabase();
                    await db.KeyDeleteAsync(keys.ToArray());
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error al invalidar claves con prefijo '{Prefix}' en Redis.", prefix);
        }
    }

    public async Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return false;
        }

        var db = GetDatabase();
        if (db is null)
        {
            return false;
        }

        try
        {
            return await db.KeyExistsAsync(key);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error al verificar existencia de la clave '{Key}' en Redis.", key);
            return false;
        }
    }
}
