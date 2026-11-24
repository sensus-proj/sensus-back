using Sensus.Domain.Common;
using Sensus.Domain.Entities;

namespace Sensus.Application.Common.Interfaces;

/// <summary>Alcance de privacidad de una URL de entrega (Spec 023).</summary>
public enum MediaDeliveryScope
{
    Public = 0,
    Private = 1
}

/// <summary>URL firmada y de corta vigencia para entregar un activo multimedia (FR-023-02).</summary>
public sealed record MediaDeliveryUrl(string Url, DateTime ExpiresAtUtc, MediaDeliveryScope Scope);

/// <summary>
/// Emite y valida URLs firmadas con HMAC-SHA256 para la entrega de multimedia (Spec 023).
/// Ref: FR-023-02 - firmar URLs cuando el contenido es privado; CA-023-02 - rechazo sin firma válida.
/// </summary>
public interface IMediaDeliveryUrlGenerator
{
    /// <summary>Genera la URL firmada del activo con la vigencia indicada.</summary>
    Result<MediaDeliveryUrl> Generate(MediaAsset asset, TimeSpan ttl);

    /// <summary>
    /// Verifica en tiempo constante la firma de una URL para el alcance de privacidad esperado.
    /// </summary>
    bool Validate(Guid mediaId, long expiresUnixSeconds, string? signature, MediaDeliveryScope scope);
}
