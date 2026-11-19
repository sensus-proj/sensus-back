namespace Sensus.Domain.Repositories;

public interface INotificationLocationRepository
{
    Task SaveAsync(Guid userId, double latitude, double longitude, DateTime sharedAtUtc, DateTime expiresAtUtc,
        CancellationToken cancellationToken);
    Task DeleteAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>
    /// FR-044-08: usuarios con ubicación vigente dentro de <paramref name="maxDistanceKm"/>, del más cercano al más lejano.
    /// </summary>
    Task<IReadOnlyList<NotificationLocationMatch>> GetActiveNearAsync(double latitude, double longitude,
        double maxDistanceKm, DateTime nowUtc, int limit, CancellationToken cancellationToken);
}

public sealed record NotificationLocationMatch(Guid UserId, double DistanceKm);
