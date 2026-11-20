using Sensus.Domain.Entities;

namespace Sensus.Domain.Repositories;

/// <summary>
/// Persistencia de las categorías de interés del usuario (Spec 054).
/// </summary>
public interface IUserInterestsRepository
{
    Task<UserInterests?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task UpsertAsync(UserInterests interests, CancellationToken cancellationToken = default);
    Task DeleteByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
}
