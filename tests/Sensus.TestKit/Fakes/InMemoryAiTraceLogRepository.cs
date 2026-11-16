using System.Collections.Concurrent;
using Sensus.Domain.Entities;
using Sensus.Domain.Repositories;

namespace Sensus.TestKit.Fakes;

public sealed class InMemoryAiTraceLogRepository : IAiTraceLogRepository
{
    private readonly ConcurrentDictionary<Guid, AiTraceLog> _store = new();

    public Task AddAsync(AiTraceLog traceLog, CancellationToken cancellationToken = default)
    {
        _store[traceLog.Id] = traceLog;
        return Task.CompletedTask;
    }

    public Task DeleteByConversationIdAsync(Guid conversationId, CancellationToken cancellationToken = default)
    {
        var keysToRemove = _store.Values
            .Where(t => t.ConversationId == conversationId)
            .Select(t => t.Id)
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
            .Where(t => t.OwnerUserId == userId)
            .Select(t => t.Id)
            .ToList();

        foreach (var key in keysToRemove)
        {
            _store.TryRemove(key, out _);
        }

        return Task.CompletedTask;
    }
}
