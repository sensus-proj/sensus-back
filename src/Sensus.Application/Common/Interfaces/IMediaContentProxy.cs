using Sensus.Domain.Common;
using Sensus.Domain.Entities;

namespace Sensus.Application.Common.Interfaces;

/// <summary>Flujo de contenido multimedia listo para servirse al cliente.</summary>
public sealed record MediaContentPayload(Stream Content, string ContentType);

/// <summary>
/// Abre el contenido de un activo privado para servirlo a través de la API sin exponer la URL de origen (Spec 023).
/// Ref: FR-023-01 - entrega de multimedia; FR-023-02 - contenido privado solo tras validar la firma.
/// </summary>
public interface IMediaContentProxy
{
    Task<Result<MediaContentPayload>> OpenReadAsync(
        MediaAsset asset,
        CancellationToken cancellationToken = default);
}
