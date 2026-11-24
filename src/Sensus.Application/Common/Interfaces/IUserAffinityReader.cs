namespace Sensus.Application.Common.Interfaces;

/// <summary>
/// Obtiene las puntuaciones normalizadas [0, 1] de afinidad por categoría inferida a partir de
/// vistas (032) y apartados (033) recientes del usuario, sin exponer PII (Spec 042, FR-042-04).
/// </summary>
public interface IUserAffinityReader
{
    Task<IReadOnlyDictionary<string, double>> GetCategoryAffinitiesAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}
