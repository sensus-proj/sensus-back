using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using Sensus.Domain.Entities;
using Sensus.Domain.Repositories;
using Sensus.Infrastructure.Persistence.Mongo.Documents;

namespace Sensus.Infrastructure.Persistence.Mongo.Repositories;

/// <summary>
/// Repositorio MongoDB para trazas de auditoría de Sensi IA con TTL indexado y purga ante baja de cuenta (FR-046-14, FR-046-15).
/// </summary>
public sealed class MongoAiTraceLogRepository : IAiTraceLogRepository
{
    private readonly MongoDbContext _context;
    private readonly ILogger<MongoAiTraceLogRepository> _logger;

    public MongoAiTraceLogRepository(
        MongoDbContext context,
        ILogger<MongoAiTraceLogRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task AddAsync(AiTraceLog traceLog, CancellationToken cancellationToken = default)
    {
        var doc = AiTraceLogDocument.FromDomain(traceLog);
        await _context.AiTraceLogs.InsertOneAsync(doc, cancellationToken: cancellationToken);
    }

    public async Task DeleteByConversationIdAsync(Guid conversationId, CancellationToken cancellationToken = default)
    {
        var filter = Builders<AiTraceLogDocument>.Filter.Eq(t => t.ConversationId, conversationId);
        await _context.AiTraceLogs.DeleteManyAsync(filter, cancellationToken);
    }

    public async Task PurgeAllByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var filter = Builders<AiTraceLogDocument>.Filter.Eq(t => t.OwnerUserId, userId);
        await _context.AiTraceLogs.DeleteManyAsync(filter, cancellationToken);
    }
}
