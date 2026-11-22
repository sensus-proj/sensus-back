using System.Collections.Concurrent;
using Sensus.Domain.Repositories;

namespace Sensus.TestKit.Fakes;

public sealed class InMemoryNotificationLocationRepository : INotificationLocationRepository
{
    private readonly ConcurrentDictionary<Guid, (double Latitude, double Longitude, DateTime ExpiresAt)> _locations = new();

    public Task SaveAsync(Guid userId, double latitude, double longitude, DateTime sharedAtUtc, DateTime expiresAtUtc,
        CancellationToken cancellationToken)
    {
        _locations[userId] = (latitude, longitude, expiresAtUtc);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid userId, CancellationToken cancellationToken)
    {
        _locations.TryRemove(userId, out _);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<NotificationLocationMatch>> GetActiveNearAsync(double latitude, double longitude,
        double maxDistanceKm, DateTime nowUtc, int limit, CancellationToken cancellationToken)
    {
        IReadOnlyList<NotificationLocationMatch> matches = _locations
            .Where(l => l.Value.ExpiresAt > nowUtc)
            .Select(l => new NotificationLocationMatch(l.Key, HaversineKm(latitude, longitude, l.Value.Latitude, l.Value.Longitude)))
            .Where(m => m.DistanceKm <= maxDistanceKm)
            .OrderBy(m => m.DistanceKm)
            .Take(limit)
            .ToList();
        return Task.FromResult(matches);
    }

    private static double HaversineKm(double lat1, double lon1, double lat2, double lon2)
    {
        const double earthRadiusKm = 6378.1;
        double dLat = (lat2 - lat1) * Math.PI / 180d;
        double dLon = (lon2 - lon1) * Math.PI / 180d;
        double a = Math.Pow(Math.Sin(dLat / 2), 2) +
            Math.Cos(lat1 * Math.PI / 180d) * Math.Cos(lat2 * Math.PI / 180d) * Math.Pow(Math.Sin(dLon / 2), 2);
        return 2 * earthRadiusKm * Math.Asin(Math.Sqrt(a));
    }
}
