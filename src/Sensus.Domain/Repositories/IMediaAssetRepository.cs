using Sensus.Domain.Entities;

namespace Sensus.Domain.Repositories;

public interface IMediaAssetRepository
{
    Task AddAsync(MediaAsset mediaAsset, CancellationToken cancellationToken = default);
    Task<int> CountByUserAndDateAsync(Guid userId, DateTime dateUtc, CancellationToken cancellationToken = default);
}
