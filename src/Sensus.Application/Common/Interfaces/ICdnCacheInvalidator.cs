namespace Sensus.Application.Common.Interfaces;

/// <summary>
/// Invalida la caché del CDN cuando un contenido se retira de circulación (Spec 023).
/// Ref: FR-023-04 - invalidar caché al retirar contenido; CA-023-03 - el contenido retirado deja de servirse.
/// </summary>
public interface ICdnCacheInvalidator
{
    /// <summary>Invalida el activo indicado. Devuelve <c>true</c> si el CDN confirmó la operación.</summary>
    Task<bool> InvalidateAsync(string publicId, CancellationToken cancellationToken = default);
}
