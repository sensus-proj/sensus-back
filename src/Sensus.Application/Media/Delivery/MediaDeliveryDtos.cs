namespace Sensus.Application.Media.Delivery;

/// <summary>
/// URL firmada lista para entregarse al cliente (Spec 023).
/// Ref: FR-023-02 - URL firmada de corta vigencia; FR-023-03 - política de caché informada.
/// </summary>
public sealed record MediaDeliveryUrlDto(
    Guid MediaId,
    string DeliveryUrl,
    DateTime ExpiresAtUtc,
    string Scope,
    string Status,
    int CacheMaxAgeSeconds);

/// <summary>
/// Resultado de la entrega: redirección al CDN (contenido aprobado) o flujo servido por la API
/// (contenido privado en cuarentena). Ref: FR-023-01, FR-023-02.
/// </summary>
public sealed record MediaDeliveryResponse(
    bool IsRedirect,
    string? RedirectUrl,
    Stream? Content,
    string ContentType,
    string CacheControl)
{
    /// <summary>Ref: CA-023-01 - contenido público aprobado servido desde el CDN.</summary>
    public static MediaDeliveryResponse Redirect(string url, string contentType, string cacheControl) =>
        new(true, url, null, contentType, cacheControl);

    /// <summary>Ref: FR-023-02 - contenido privado servido por la API sin caché compartida.</summary>
    public static MediaDeliveryResponse Streamed(Stream content, string contentType, string cacheControl) =>
        new(false, null, content, contentType, cacheControl);
}
