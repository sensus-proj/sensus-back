using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using Sensus.Domain.Entities;
using Sensus.Domain.Repositories;
using Sensus.Infrastructure.Persistence.Mongo.Documents;

namespace Sensus.Infrastructure.Persistence.Mongo.Repositories;

/// <summary>
/// Implementación del repositorio de dispositivos para notificaciones push con MongoDB Atlas (Spec 027).
/// </summary>
public sealed class MongoDeviceRepository : IDeviceRepository
{
    private readonly IMongoCollection<DeviceDocument> _collection;
    private readonly ILogger<MongoDeviceRepository> _logger;

    public MongoDeviceRepository(
        MongoDbContext context,
        ILogger<MongoDeviceRepository> logger)
    {
        _collection = context.Devices;
        _logger = logger;
    }

    public async Task AddAsync(Device device, CancellationToken cancellationToken = default)
    {
        DeviceDocument document = DeviceDocument.FromDomain(device);
        await _collection.InsertOneAsync(document, cancellationToken: cancellationToken);
    }

    public async Task<Device?> GetByTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        FilterDefinition<DeviceDocument> filter = Builders<DeviceDocument>.Filter.Eq(d => d.Token, token);
        DeviceDocument? document = await _collection.Find(filter).FirstOrDefaultAsync(cancellationToken);
        return document?.ToDomain();
    }

    public async Task<Device?> GetByUserAndDeviceIdAsync(Guid userId, string deviceId, CancellationToken cancellationToken = default)
    {
        FilterDefinition<DeviceDocument> filter = Builders<DeviceDocument>.Filter.And(
            Builders<DeviceDocument>.Filter.Eq(d => d.UserId, userId),
            Builders<DeviceDocument>.Filter.Eq(d => d.DeviceId, deviceId));
        DeviceDocument? document = await _collection.Find(filter).FirstOrDefaultAsync(cancellationToken);
        return document?.ToDomain();
    }

    public async Task<Device?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        FilterDefinition<DeviceDocument> filter = Builders<DeviceDocument>.Filter.Eq(d => d.Id, id);
        DeviceDocument? document = await _collection.Find(filter).FirstOrDefaultAsync(cancellationToken);
        return document?.ToDomain();
    }

    public async Task UpdateAsync(Device device, CancellationToken cancellationToken = default)
    {
        DeviceDocument document = DeviceDocument.FromDomain(device);
        FilterDefinition<DeviceDocument> filter = Builders<DeviceDocument>.Filter.Eq(d => d.Id, device.Id);
        await _collection.ReplaceOneAsync(filter, document, cancellationToken: cancellationToken);
    }

    public async Task<bool> ExistsForUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        FilterDefinition<DeviceDocument> filter = Builders<DeviceDocument>.Filter.Eq(d => d.UserId, userId);
        return await _collection.CountDocumentsAsync(filter, cancellationToken: cancellationToken) > 0;
    }

    public async Task<List<Device>> GetActiveByUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        FilterDefinition<DeviceDocument> filter = Builders<DeviceDocument>.Filter.And(
            Builders<DeviceDocument>.Filter.Eq(d => d.UserId, userId),
            Builders<DeviceDocument>.Filter.Eq(d => d.IsActive, true));
        List<DeviceDocument> documents = await _collection.Find(filter).ToListAsync(cancellationToken);
        return documents.Select(d => d.ToDomain()).ToList();
    }
}
