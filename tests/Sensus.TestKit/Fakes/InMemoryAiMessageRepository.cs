using System.Collections.Concurrent;
using Sensus.Domain.Entities;
using Sensus.Domain.Repositories;

namespace Sensus.TestKit.Fakes;

public sealed class InMemoryAiMessageRepository : IAiMessageRepository
{
    private readonly ConcurrentDictionary<Guid, AiMessage> _store = new();

    public Task<AiMessage?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _store.TryGetValue(id, out var msg);
        return Task.FromResult(msg);
    }

    public Task<IReadOnlyList<AiMessage>> GetPagedByConversationIdAsync(
        Guid conversationId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var messages = _store.Values
            .Where(m => m.ConversationId == conversationId)
            .OrderBy(m => m.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return Task.FromResult<IReadOnlyList<AiMessage>>(messages);
    }

    public Task<IReadOnlyList<AiMessage>> GetRecentByConversationIdAsync(
        Guid conversationId,
        int limit = 10,
        CancellationToken cancellationToken = default)
    {
        var messages = _store.Values
            .Where(m => m.ConversationId == conversationId)
            .OrderBy(m => m.CreatedAtUtc)
            .TakeLast(limit)
            .ToList();

        return Task.FromResult<IReadOnlyList<AiMessage>>(messages);
    }

    public Task<int> CountByConversationIdAsync(Guid conversationId, CancellationToken cancellationToken = default)
    {
        var count = _store.Values.Count(m => m.ConversationId == conversationId);
        return Task.FromResult(count);
    }

    public Task AddAsync(AiMessage message, CancellationToken cancellationToken = default)
    {
        _store[message.Id] = message;
        return Task.CompletedTask;
    }

    public Task DeleteByConversationIdAsync(Guid conversationId, CancellationToken cancellationToken = default)
    {
        var keysToRemove = _store.Values
            .Where(m => m.ConversationId == conversationId)
            .Select(m => m.Id)
            .ToList();

        foreach (var key in keysToRemove)
        {
            _store.TryRemove(key, out _);
        }

        return Task.CompletedTask;
    }

    public Task PurgeAllByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var keysToRemove = _store.Values
            .Where(m => m.OwnerUserId == userId)
            .Select(m => m.Id)
            .ToList();

        foreach (var key in keysToRemove)
        {
            _store.TryRemove(key, out _);
        }

        return Task.CompletedTask;
    }
}
