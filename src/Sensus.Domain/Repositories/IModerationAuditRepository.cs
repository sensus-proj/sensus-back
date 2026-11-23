using Sensus.Domain.Entities;

namespace Sensus.Domain.Repositories;

/// <summary>
/// Persistencia de la auditoría anonimizada de moderación (Spec 061, FR-061-05).
/// La colección es de solo adición y su retención se gestiona mediante TTL en MongoDB.
/// </summary>
public interface IModerationAuditRepository
{
    /// <summary>
    /// Inserta la entrada si no existe otra con el mismo identificador determinista.
    /// Devuelve <c>true</c> solo cuando se insertó una entrada nueva (idempotencia, FR-061-06).
    /// </summary>
    Task<bool> AddIfNotExistsAsync(
        ModerationAudit entry,
        CancellationToken cancellationToken = default);
}
