using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using Sensus.Domain.Entities;
using Sensus.Domain.Repositories;
using Sensus.Infrastructure.Persistence.Mongo.Documents;

namespace Sensus.Infrastructure.Persistence.Mongo.Repositories;

/// <summary>
/// Repositorio MongoDB para mensajes de Sensi IA con paginación y orden cronológico (FR-046-01, FR-046-15).
/// </summary>
public sealed class MongoAiMessageRepository : IAiMessageRepository
{
    private readonly MongoDbContext _context;
    private readonly ILogger<MongoAiMessageRepository> _logger;

    public MongoAiMessageRepository(
        MongoDbContext context,
        ILogger<MongoAiMessageRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<AiMessage?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var filter = Builders<AiMessageDocument>.Filter.Eq(m => m.Id, id);
        var doc = await _context.AiMessages.Find(filter).FirstOrDefaultAsync(cancellationToken);
        return doc?.ToDomain();
    }

    public async Task<IReadOnlyList<AiMessage>> GetPagedByConversationIdAsync(
        Guid conversationId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var filter = Builders<AiMessageDocument>.Filter.Eq(m => m.ConversationId, conversationId);
        var sort = Builders<AiMessageDocument>.Sort.Descending(m => m.CreatedAtUtc);

        var safePage = Math.Max(1, page);
        var safePageSize = Math.Clamp(pageSize, 1, 100);

        var docs = await _context.AiMessages
            .Find(filter)
            .Sort(sort)
            .Skip((safePage - 1) * safePageSize)
            .Limit(safePageSize)
            .ToListAsync(cancellationToken);

        // Retorna ordenados ascendentemente para visualización natural de chat
        return docs.Select(d => d.ToDomain()).OrderBy(m => m.CreatedAtUtc).ToList();
    }

    public async Task<IReadOnlyList<AiMessage>> GetRecentByConversationIdAsync(
        Guid conversationId,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var filter = Builders<AiMessageDocument>.Filter.Eq(m => m.ConversationId, conversationId);
        var sort = Builders<AiMessageDocument>.Sort.Descending(m => m.CreatedAtUtc);
        var safeLimit = Math.Clamp(limit, 1, 20);

        var docs = await _context.AiMessages
            .Find(filter)
            .Sort(sort)
            .Limit(safeLimit)
            .ToListAsync(cancellationToken);

        // Retorna orden cronológico (más antiguo al más reciente) para inyectar en el prompt
        return docs.Select(d => d.ToDomain()).OrderBy(m => m.CreatedAtUtc).ToList();
    }

    public async Task<int> CountByConversationIdAsync(Guid conversationId, CancellationToken cancellationToken = default)
    {
        var filter = Builders<AiMessageDocument>.Filter.Eq(m => m.ConversationId, conversationId);
        return (int)await _context.AiMessages.CountDocumentsAsync(filter, cancellationToken: cancellationToken);
    }

    public async Task AddAsync(AiMessage message, CancellationToken cancellationToken = default)
    {
        var doc = AiMessageDocument.FromDomain(message);
        await _context.AiMessages.InsertOneAsync(doc, cancellationToken: cancellationToken);
    }

    public async Task DeleteByConversationIdAsync(Guid conversationId, CancellationToken cancellationToken = default)
    {
        var filter = Builders<AiMessageDocument>.Filter.Eq(m => m.ConversationId, conversationId);
        await _context.AiMessages.DeleteManyAsync(filter, cancellationToken);
    }

    public async Task PurgeAllByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var filter = Builders<AiMessageDocument>.Filter.Eq(m => m.OwnerUserId, userId);
        await _context.AiMessages.DeleteManyAsync(filter, cancellationToken);
    }
}
