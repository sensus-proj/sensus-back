using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using Sensus.Domain.Entities;
using Sensus.Domain.Repositories;
using Sensus.Infrastructure.Persistence.Mongo.Documents;

namespace Sensus.Infrastructure.Persistence.Mongo.Repositories;

/// <summary>
/// Implementación del repositorio de preferencias de notificación con MongoDB Atlas (Spec 044).
/// </summary>
public sealed class MongoNotificationSettingsRepository : INotificationSettingsRepository
{
    private readonly IMongoCollection<NotificationSettingsDocument> _collection;
    private readonly ILogger<MongoNotificationSettingsRepository> _logger;

    public MongoNotificationSettingsRepository(
        MongoDbContext context,
        ILogger<MongoNotificationSettingsRepository> logger)
    {
        _collection = context.NotificationSettings;
        _logger = logger;
    }

    public async Task<NotificationSettings?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var filter = Builders<NotificationSettingsDocument>.Filter.Eq(d => d.UserId, userId);
        var document = await _collection.Find(filter).FirstOrDefaultAsync(cancellationToken);
        return document?.ToDomain();
    }

    public async Task AddAsync(NotificationSettings settings, CancellationToken cancellationToken = default)
    {
        var document = NotificationSettingsDocument.FromDomain(settings);
        await _collection.InsertOneAsync(document, cancellationToken: cancellationToken);
    }

    public async Task UpdateAsync(NotificationSettings settings, CancellationToken cancellationToken = default)
    {
        var document = NotificationSettingsDocument.FromDomain(settings);
        var filter = Builders<NotificationSettingsDocument>.Filter.Eq(d => d.UserId, settings.UserId);
        await _collection.ReplaceOneAsync(filter, document, cancellationToken: cancellationToken);
    }
}
