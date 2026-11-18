using System.Collections.Concurrent;
using Sensus.Domain.Entities;
using Sensus.Domain.Enums;
using Sensus.Domain.Repositories;

namespace Sensus.TestKit.Fakes;

public sealed class InMemoryAiConversationRepository : IAiConversationRepository
{
    private readonly ConcurrentDictionary<Guid, AiConversation> _store = new();

    public Task<AiConversation?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _store.TryGetValue(id, out var conv);
        return Task.FromResult(conv);
    }

    public Task<AiConversation?> GetActiveByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var active = _store.Values
            .Where(c => c.OwnerUserId == userId && c.Status == AiConversationStatus.Active)
            .OrderByDescending(c => c.LastModifiedAtUtc ?? c.CreatedAtUtc)
            .FirstOrDefault();

        return Task.FromResult(active);
    }

    public Task AddAsync(AiConversation conversation, CancellationToken cancellationToken = default)
    {
        _store[conversation.Id] = conversation;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(AiConversation conversation, CancellationToken cancellationToken = default)
    {
        _store[conversation.Id] = conversation;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _store.TryRemove(id, out _);
        return Task.CompletedTask;
    }

    public Task PurgeAllByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var keysToRemove = _store.Values
            .Where(c => c.OwnerUserId == userId)
            .Select(c => c.Id)
            .ToList();

        foreach (var key in keysToRemove)
        {
            _store.TryRemove(key, out _);
        }

        return Task.CompletedTask;
    }
}
