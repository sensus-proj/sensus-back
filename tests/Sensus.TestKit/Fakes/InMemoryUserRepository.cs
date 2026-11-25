using System.Collections.Concurrent;
using Sensus.Domain.Common;
using Sensus.Domain.Entities;
using Sensus.Domain.Repositories;

namespace Sensus.TestKit.Fakes;

public class InMemoryUserRepository : IUserRepository
{
    private readonly ConcurrentDictionary<Guid, User> _users = new();

    public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _users.TryGetValue(id, out var user);
        return Task.FromResult(user);
    }

    public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        var user = _users.Values.FirstOrDefault(u => string.Equals(u.Correo, email, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(user);
    }

    public Task<User?> GetByInviteCodeAsync(string inviteCode, CancellationToken cancellationToken = default)
    {
        string normalizedCode = inviteCode.Trim().ToUpperInvariant();
        User? user = _users.Values.FirstOrDefault(u =>
            u.Estatus &&
            string.Equals(
                FriendInviteCodeGenerator.Generate(u.Correo, u.NombreCompleto),
                normalizedCode,
                StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(user);
    }

    public Task<bool> ExistsByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        var exists = _users.Values.Any(u => string.Equals(u.Correo, email, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(exists);
    }

    public Task AddAsync(User user, CancellationToken cancellationToken = default)
    {
        _users[user.Id] = user;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(User user, CancellationToken cancellationToken = default)
    {
        _users[user.Id] = user;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _users.TryRemove(id, out _);
        return Task.CompletedTask;
    }

    public Task<bool> MarkAsDeletedAsync(
        Guid id,
        DateTime deletedAtUtc,
        string securityStamp,
        CancellationToken cancellationToken = default)
    {
        if (!_users.TryGetValue(id, out User? user) || user.IsDeleted)
        {
            return Task.FromResult(false);
        }

        user.MarkAsDeleted(deletedAtUtc);
        typeof(User).GetProperty(nameof(User.SecurityStamp))?.SetValue(user, securityStamp);
        return Task.FromResult(true);
    }

    public Task<IReadOnlyList<Guid>> GetIdsDeletedBeforeAsync(
        DateTime cutoffUtc,
        int limit,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<Guid> ids = _users.Values
            .Where(u => u.IsDeleted && u.DeletedAtUtc <= cutoffUtc)
            .Take(Math.Clamp(limit, 1, 100))
            .Select(u => u.Id)
            .ToList();
        return Task.FromResult(ids);
    }

    public Task<bool> PurgeDeletedAsync(
        Guid id,
        DateTime cutoffUtc,
        CancellationToken cancellationToken = default)
    {
        if (!_users.TryGetValue(id, out User? user) ||
            !user.IsDeleted ||
            user.DeletedAtUtc > cutoffUtc)
        {
            return Task.FromResult(false);
        }

        return Task.FromResult(_users.TryRemove(id, out _));
    }
}
