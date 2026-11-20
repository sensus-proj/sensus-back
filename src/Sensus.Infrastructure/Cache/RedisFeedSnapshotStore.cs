using Microsoft.Extensions.Logging;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Offers;

namespace Sensus.Infrastructure.Cache;

/// <summary>
/// Almacena y recupera instantáneas ordenadas del feed en Redis bajo <c>feed:offers:{userId}:{snapshotId}</c>
/// con degradación controlada ante indisponibilidad de caché (Spec 042, FR-042-08, plan §5).
/// </summary>
public sealed class RedisFeedSnapshotStore : IFeedSnapshotStore
{
    private readonly ICacheService _cacheService;
    private readonly ILogger<RedisFeedSnapshotStore> _logger;

    public RedisFeedSnapshotStore(
        ICacheService cacheService,
        ILogger<RedisFeedSnapshotStore> logger)
    {
        _cacheService = cacheService;
        _logger = logger;
    }

    public async Task<FeedSnapshot?> GetAsync(
        Guid userId,
        Guid snapshotId,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty || snapshotId == Guid.Empty || !_cacheService.IsAvailable)
        {
            return null;
        }

        try
        {
            return await _cacheService.GetAsync<FeedSnapshot>(
                FeedCacheKeys.Snapshot(userId, snapshotId),
                cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Redis degradado al leer instantánea del feed; regenerando ranking.");
            return null;
        }
    }

    public async Task<bool> SaveAsync(
        Guid userId,
        FeedSnapshot snapshot,
        TimeSpan ttl,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (userId == Guid.Empty || snapshot.SnapshotId == Guid.Empty || !_cacheService.IsAvailable)
        {
            return false;
        }

        try
        {
            await _cacheService.SetAsync(
                FeedCacheKeys.Snapshot(userId, snapshot.SnapshotId),
                snapshot,
                ttl,
                cancellationToken);
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Redis degradado al guardar instantánea del feed; respondiendo snapshotId nulo.");
            return false;
        }
    }
}
