using Sensus.Domain.Entities;
using Sensus.Domain.Repositories;

namespace Sensus.TestKit.Fakes;

public sealed class InMemoryMediaAssetRepository : IMediaAssetRepository
{
    private readonly List<MediaAsset> _assets = [];
    private readonly Lock _lock = new();

    public Task AddAsync(MediaAsset mediaAsset, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            _assets.Add(mediaAsset);
        }

        return Task.CompletedTask;
    }

    public Task<int> CountByUserAndDateAsync(Guid userId, DateTime dateUtc, CancellationToken cancellationToken = default)
    {
        DateTime start = dateUtc.Date;
        DateTime end = start.AddDays(1);
        lock (_lock)
        {
            return Task.FromResult(_assets.Count(a => a.OwnerId == userId && a.CreatedAtUtc >= start && a.CreatedAtUtc < end));
        }
    }

    public Task<MediaAsset?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            return Task.FromResult(_assets.FirstOrDefault(a => a.Id == id));
        }
    }

    public Task<bool> HasApprovedImageAsync(
        Guid ownerId,
        string secureUrl,
        string moderatedTextFingerprint,
        CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            return Task.FromResult(_assets.Any(a =>
                a.OwnerId == ownerId &&
                a.SecureUrl == secureUrl &&
                a.Status == MediaAssetStatus.Approved &&
                a.ModeratedTextFingerprint == moderatedTextFingerprint));
        }
    }

    public Task<bool> IsApprovedOwnedUrlAsync(
        Guid ownerId,
        string secureUrl,
        CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            return Task.FromResult(_assets.Any(a =>
                a.OwnerId == ownerId &&
                a.SecureUrl == secureUrl &&
                a.Status == MediaAssetStatus.Approved));
        }
    }

    public Task<IReadOnlyList<MediaAsset>> GetQuarantinedCreatedBeforeAsync(
        DateTime cutoffUtc,
        int limit,
        CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            IReadOnlyList<MediaAsset> result = _assets
                .Where(a => a.Status == MediaAssetStatus.Quarantined && a.CreatedAtUtc < cutoffUtc)
                .OrderBy(a => a.CreatedAtUtc)
                .Take(limit)
                .ToList();
            return Task.FromResult(result);
        }
    }

    public Task UpdateAsync(MediaAsset mediaAsset, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            int idx = _assets.FindIndex(a => a.Id == mediaAsset.Id);
            if (idx >= 0)
            {
                _assets[idx] = mediaAsset;
            }
        }

        return Task.CompletedTask;
    }
}
