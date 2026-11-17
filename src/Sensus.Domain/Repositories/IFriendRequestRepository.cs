using Sensus.Domain.Entities;

namespace Sensus.Domain.Repositories;

/// <summary>
/// Repositorio para solicitudes de amistad y conexiones vecinales aceptadas.
/// </summary>
public interface IFriendRequestRepository
{
    Task<FriendRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<FriendRequest?> GetBetweenUsersAsync(Guid userA, Guid userB, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FriendRequest>> GetForUserAsync(Guid userId, CancellationToken cancellationToken = default);
    Task AddAsync(FriendRequest request, CancellationToken cancellationToken = default);
    Task UpdateAsync(FriendRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
