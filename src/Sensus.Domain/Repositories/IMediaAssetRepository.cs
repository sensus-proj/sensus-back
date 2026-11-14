using Sensus.Domain.Entities;

namespace Sensus.Domain.Repositories;

public interface IMediaAssetRepository
{
    Task AddAsync(MediaAsset mediaAsset, CancellationToken cancellationToken = default);
    Task<int> CountByUserAndDateAsync(Guid userId, DateTime dateUtc, CancellationToken cancellationToken = default);

    /// <summary>
    /// Activos en cuarentena creados antes del instante indicado, ordenados del más antiguo al más reciente (FR-019-03).
    /// </summary>
    Task<IReadOnlyList<MediaAsset>> GetQuarantinedCreatedBeforeAsync(
        DateTime cutoffUtc,
        int limit,
        CancellationToken cancellationToken = default);

    Task UpdateAsync(MediaAsset mediaAsset, CancellationToken cancellationToken = default);
}
