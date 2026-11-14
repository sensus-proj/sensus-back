using Sensus.Domain.Entities;

namespace Sensus.Domain.Repositories;

public interface IDeviceRepository
{
    Task AddAsync(Device device, CancellationToken cancellationToken = default);
    Task<Device?> GetByTokenAsync(string token, CancellationToken cancellationToken = default);
    Task<Device?> GetByUserAndDeviceIdAsync(Guid userId, string deviceId, CancellationToken cancellationToken = default);
    Task<Device?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task UpdateAsync(Device device, CancellationToken cancellationToken = default);
    Task<bool> ExistsForUserAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<List<Device>> GetActiveByUserAsync(Guid userId, CancellationToken cancellationToken = default);
}
