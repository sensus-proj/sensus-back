namespace Sensus.Application.Common.Interfaces;

/// <summary>
/// Contrato para el servicio de caché distribuido (Spec 048).
/// </summary>
public interface ICacheService
{
    /// <summary>
    /// Obtiene un elemento del caché deserializado al tipo especificado.
    /// Devuelve default(T) si no existe o si el servicio de caché no está disponible.
    /// </summary>
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Guarda un elemento en caché con un tiempo de expiración opcional.
    /// Si el servicio de caché no está disponible, la operación se omite de forma transparente.
    /// </summary>
    Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Guarda un elemento en caché usando el TTL configurado para la entidad (appsettings: Redis:TtlByEntityMinutes).
    /// Evita TTL hardcodeados en la capa de aplicación (FR-048-10).
    /// </summary>
    Task SetForEntityAsync<T>(string key, T value, string entityName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Elimina una clave del caché.
    /// </summary>
    Task RemoveAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Elimina múltiples claves que inicien con el prefijo especificado.
    /// </summary>
    Task RemoveByPrefixAsync(string prefix, CancellationToken cancellationToken = default);

    /// <summary>
    /// Determina si una clave existe en el caché.
    /// </summary>
    Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Indica si el servicio de caché distribuido está habilitado y conectado.
    /// </summary>
    bool IsAvailable { get; }

    /// <summary>
    /// Proveedor activo: <c>redis</c>, <c>memory</c> o <c>none</c> (Spec 048, FR-048-12).
    /// </summary>
    string Provider { get; }

    /// <summary>
    /// Incrementa atómicamente un valor de punto flotante en caché (ej. INCRBYFLOAT).
    /// Devuelve el nuevo valor o null si el servicio no está disponible.
    /// </summary>
    Task<double?> IncrementByFloatAsync(string key, double value, TimeSpan? expiration = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Incrementa atómicamente un contador entero en caché (ej. INCRBY).
    /// Devuelve el nuevo valor o null si el servicio no está disponible.
    /// </summary>
    Task<long?> IncrementAsync(string key, long value = 1, TimeSpan? expiration = null, CancellationToken cancellationToken = default);
}
