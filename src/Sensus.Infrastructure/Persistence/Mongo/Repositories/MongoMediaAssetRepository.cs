using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using Sensus.Domain.Entities;
using Sensus.Domain.Repositories;
using Sensus.Infrastructure.Persistence.Mongo.Documents;

namespace Sensus.Infrastructure.Persistence.Mongo.Repositories;

/// <summary>
/// Implementación del repositorio de activos multimedia con MongoDB Atlas (Spec 022).
/// </summary>
public sealed class MongoMediaAssetRepository : IMediaAssetRepository
{
    private readonly IMongoCollection<MediaAssetDocument> _collection;
    private readonly ILogger<MongoMediaAssetRepository> _logger;

    public MongoMediaAssetRepository(
        MongoDbContext context,
        ILogger<MongoMediaAssetRepository> logger)
    {
        _collection = context.MediaAssets;
        _logger = logger;
    }

    public async Task AddAsync(MediaAsset mediaAsset, CancellationToken cancellationToken = default)
    {
        MediaAssetDocument document = MediaAssetDocument.FromDomain(mediaAsset);
        await _collection.InsertOneAsync(document, cancellationToken: cancellationToken);
    }

    public async Task<int> CountByUserAndDateAsync(
        Guid userId,
        DateTime dateUtc,
        CancellationToken cancellationToken = default)
    {
        DateTime startOfDay = dateUtc.Date;
        DateTime endOfDay = startOfDay.AddDays(1);

        FilterDefinition<MediaAssetDocument> filter = Builders<MediaAssetDocument>.Filter.And(
            Builders<MediaAssetDocument>.Filter.Eq(d => d.OwnerId, userId),
            Builders<MediaAssetDocument>.Filter.Gte(d => d.CreatedAtUtc, startOfDay),
            Builders<MediaAssetDocument>.Filter.Lt(d => d.CreatedAtUtc, endOfDay));

        return (int)await _collection.CountDocumentsAsync(filter, cancellationToken: cancellationToken);
    }
}
