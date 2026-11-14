using Sensus.Domain.Entities;

namespace Sensus.Domain.Repositories;

public interface INotificationSettingsRepository
{
    Task<NotificationSettings?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task AddAsync(NotificationSettings settings, CancellationToken cancellationToken = default);
    Task UpdateAsync(NotificationSettings settings, CancellationToken cancellationToken = default);
}
