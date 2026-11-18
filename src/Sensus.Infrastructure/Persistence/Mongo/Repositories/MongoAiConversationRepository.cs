using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using Sensus.Domain.Entities;
using Sensus.Domain.Enums;
using Sensus.Domain.Repositories;
using Sensus.Infrastructure.Persistence.Mongo.Documents;

namespace Sensus.Infrastructure.Persistence.Mongo.Repositories;

/// <summary>
/// Repositorio MongoDB para conversaciones de Sensi IA con borrado en cascada (FR-046-01, FR-046-15).
/// </summary>
public sealed class MongoAiConversationRepository : IAiConversationRepository
{
    private readonly MongoDbContext _context;
    private readonly ILogger<MongoAiConversationRepository> _logger;

    public MongoAiConversationRepository(
        MongoDbContext context,
        ILogger<MongoAiConversationRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<AiConversation?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var filter = Builders<AiConversationDocument>.Filter.Eq(c => c.Id, id);
        var doc = await _context.AiConversations.Find(filter).FirstOrDefaultAsync(cancellationToken);
        return doc?.ToDomain();
    }

    public async Task<AiConversation?> GetActiveByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var filter = Builders<AiConversationDocument>.Filter.And(
            Builders<AiConversationDocument>.Filter.Eq(c => c.OwnerUserId, userId),
            Builders<AiConversationDocument>.Filter.Eq(c => c.Status, AiConversationStatus.Active.ToString()));

        var sort = Builders<AiConversationDocument>.Sort.Descending(c => c.LastModifiedAtUtc);
        var doc = await _context.AiConversations.Find(filter).Sort(sort).FirstOrDefaultAsync(cancellationToken);
        return doc?.ToDomain();
    }

    public async Task AddAsync(AiConversation conversation, CancellationToken cancellationToken = default)
    {
        var doc = AiConversationDocument.FromDomain(conversation);
        await _context.AiConversations.InsertOneAsync(doc, cancellationToken: cancellationToken);
    }

    public async Task UpdateAsync(AiConversation conversation, CancellationToken cancellationToken = default)
    {
        var doc = AiConversationDocument.FromDomain(conversation);
        var filter = Builders<AiConversationDocument>.Filter.Eq(c => c.Id, conversation.Id);
        await _context.AiConversations.ReplaceOneAsync(filter, doc, cancellationToken: cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        // Borrado en cascada: conversación, mensajes y trazas asociadas
        var convFilter = Builders<AiConversationDocument>.Filter.Eq(c => c.Id, id);
        await _context.AiConversations.DeleteOneAsync(convFilter, cancellationToken);

        var msgFilter = Builders<AiMessageDocument>.Filter.Eq(m => m.ConversationId, id);
        await _context.AiMessages.DeleteManyAsync(msgFilter, cancellationToken);

        var traceFilter = Builders<AiTraceLogDocument>.Filter.Eq(t => t.ConversationId, id);
        await _context.AiTraceLogs.DeleteManyAsync(traceFilter, cancellationToken);

        _logger.LogInformation("Conversación {ConversationId} eliminada en cascada junto con sus mensajes y trazas.", id);
    }

    public async Task PurgeAllByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        // Purga integral por eliminación de cuenta (Spec 030, FR-046-15)
        var convFilter = Builders<AiConversationDocument>.Filter.Eq(c => c.OwnerUserId, userId);
        await _context.AiConversations.DeleteManyAsync(convFilter, cancellationToken);

        var msgFilter = Builders<AiMessageDocument>.Filter.Eq(m => m.OwnerUserId, userId);
        await _context.AiMessages.DeleteManyAsync(msgFilter, cancellationToken);

        var traceFilter = Builders<AiTraceLogDocument>.Filter.Eq(t => t.OwnerUserId, userId);
        await _context.AiTraceLogs.DeleteManyAsync(traceFilter, cancellationToken);

        _logger.LogInformation("Todos los datos de Sensi IA del usuario {UserId} purgados.", userId);
    }
}
