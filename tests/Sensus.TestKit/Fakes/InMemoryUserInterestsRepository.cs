using System.Collections.Concurrent;
using Sensus.Domain.Entities;
using Sensus.Domain.Repositories;

namespace Sensus.TestKit.Fakes;

/// <summary>
/// Repositorio en memoria de intereses de usuario para pruebas E2E sin MongoDB.
/// </summary>
public sealed class InMemoryUserInterestsRepository : IUserInterestsRepository
{
    private readonly ConcurrentDictionary<Guid, UserInterests> _store = new();

    public Task<UserInterests?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _store.TryGetValue(userId, out UserInterests? interests);
        return Task.FromResult(interests);
    }

    public Task UpsertAsync(UserInterests interests, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(interests);
        _store[interests.UserId] = interests;
        return Task.CompletedTask;
    }

    public Task DeleteByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _store.TryRemove(userId, out _);
        return Task.CompletedTask;
    }
}
