using Sensus.Domain.Entities;

namespace Sensus.Domain.Repositories;

public interface IAiConversationRepository
{
    Task<AiConversation?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<AiConversation?> GetActiveByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task AddAsync(AiConversation conversation, CancellationToken cancellationToken = default);
    Task UpdateAsync(AiConversation conversation, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task PurgeAllByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
}

public interface IAiMessageRepository
{
    Task<AiMessage?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AiMessage>> GetPagedByConversationIdAsync(Guid conversationId, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AiMessage>> GetRecentByConversationIdAsync(Guid conversationId, int limit, CancellationToken cancellationToken = default);
    Task<int> CountByConversationIdAsync(Guid conversationId, CancellationToken cancellationToken = default);
    Task AddAsync(AiMessage message, CancellationToken cancellationToken = default);
    Task DeleteByConversationIdAsync(Guid conversationId, CancellationToken cancellationToken = default);
    Task PurgeAllByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
}

public interface IAiTraceLogRepository
{
    Task AddAsync(AiTraceLog traceLog, CancellationToken cancellationToken = default);
    Task DeleteByConversationIdAsync(Guid conversationId, CancellationToken cancellationToken = default);
    Task PurgeAllByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
}
