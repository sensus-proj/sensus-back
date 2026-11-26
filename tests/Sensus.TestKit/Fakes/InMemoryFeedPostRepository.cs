using Sensus.Domain.Entities;
using Sensus.Domain.Enums;
using Sensus.Domain.Repositories;

namespace Sensus.TestKit.Fakes;

/// <summary>Repositorio en memoria de publicaciones del feed para pruebas sin MongoDB.</summary>
public sealed class InMemoryFeedPostRepository : IFeedPostRepository
{
    private readonly List<FeedPost> _posts = [];

    public Task AddAsync(FeedPost post, CancellationToken cancellationToken = default)
    {
        _posts.Add(post);
        return Task.CompletedTask;
    }

    public Task<FeedPost?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_posts.FirstOrDefault(p => p.Id == id));

    public Task UpdateAsync(FeedPost post, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task<IReadOnlyList<FeedPost>> GetPublishedPagedAsync(
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<FeedPost> page = _posts
            .Where(p => p.Status == FeedPostStatus.Published && !p.IsDeleted)
            .OrderByDescending(p => p.CreatedAtUtc)
            .Skip((Math.Max(1, pageNumber) - 1) * Math.Max(1, pageSize))
            .Take(Math.Max(1, pageSize))
            .ToList();
        return Task.FromResult(page);
    }

    public Task<int> CountPublishedAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(_posts.Count(p => p.Status == FeedPostStatus.Published && !p.IsDeleted));

    public Task<(IReadOnlyList<FeedPost> Items, int TotalCount)> GetPublishedNearbyPagedAsync(
        double latitude,
        double longitude,
        double radiusMeters,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        List<FeedPost> matches = _posts
            .Where(p => p.Status == FeedPostStatus.Published && !p.IsDeleted && p.HasLocation)
            .Where(p => HaversineMeters(latitude, longitude, p.Latitude!.Value, p.Longitude!.Value) <= radiusMeters)
            .OrderByDescending(p => p.CreatedAtUtc)
            .ThenByDescending(p => p.Id.ToString())
            .ToList();

        IReadOnlyList<FeedPost> page = matches
            .Skip((Math.Max(1, pageNumber) - 1) * Math.Max(1, pageSize))
            .Take(Math.Max(1, pageSize))
            .ToList();

        return Task.FromResult((page, matches.Count));
    }

    private static double HaversineMeters(double lat1, double lng1, double lat2, double lng2)
    {
        const double EarthRadiusMeters = 6_371_000d;
        double dLat = (lat2 - lat1) * Math.PI / 180d;
        double dLng = (lng2 - lng1) * Math.PI / 180d;
        double a = Math.Pow(Math.Sin(dLat / 2), 2)
            + (Math.Cos(lat1 * Math.PI / 180d) * Math.Cos(lat2 * Math.PI / 180d) * Math.Pow(Math.Sin(dLng / 2), 2));
        return 2 * EarthRadiusMeters * Math.Asin(Math.Min(1, Math.Sqrt(a)));
    }

    public Task<int> AnonymizeByAuthorAsync(
        Guid authorUserId,
        DateTime updatedAtUtc,
        CancellationToken cancellationToken = default)
    {
        int changed = _posts.Count(p => p.AuthorUserId == authorUserId && p.Anonymize(updatedAtUtc));
        return Task.FromResult(changed);
    }

    public Task<int> PurgeQuarantinedBeforeAsync(
        DateTime cutoffUtc,
        CancellationToken cancellationToken = default)
    {
        int removed = _posts.RemoveAll(p =>
            p.Status == FeedPostStatus.Quarantined && !p.IsDeleted && p.CreatedAtUtc < cutoffUtc);
        return Task.FromResult(removed);
    }
}
