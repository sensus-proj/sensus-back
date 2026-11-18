using System.Collections.Concurrent;
using Sensus.Domain.Entities;
using Sensus.Domain.Repositories;

namespace Sensus.TestKit.Fakes;

public sealed class InMemoryFriendRequestRepository : IFriendRequestRepository
{
    private readonly ConcurrentDictionary<Guid, FriendRequest> _requests = new();

    public Task<FriendRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _requests.TryGetValue(id, out var value);
        return Task.FromResult(value);
    }

    public Task<FriendRequest?> GetBetweenUsersAsync(Guid userA, Guid userB, CancellationToken cancellationToken = default)
    {
        var match = _requests.Values
            .Where(r => (r.SenderUserId == userA && r.TargetUserId == userB) ||
                        (r.SenderUserId == userB && r.TargetUserId == userA))
            .OrderByDescending(r => r.CreatedAtUtc)
            .FirstOrDefault();
        return Task.FromResult(match);
    }

    public Task<IReadOnlyList<FriendRequest>> GetForUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<FriendRequest> result = _requests.Values
            .Where(r => r.SenderUserId == userId || r.TargetUserId == userId)
            .OrderByDescending(r => r.CreatedAtUtc)
            .ToList();
        return Task.FromResult(result);
    }

    public Task AddAsync(FriendRequest request, CancellationToken cancellationToken = default)
    {
        _requests[request.Id] = request;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(FriendRequest request, CancellationToken cancellationToken = default)
    {
        _requests[request.Id] = request;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _requests.TryRemove(id, out _);
        return Task.CompletedTask;
    }
}
