using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using Sensus.Domain.Entities;
using Sensus.Domain.Enums;
using Sensus.Domain.Repositories;
using Sensus.Infrastructure.Persistence.Mongo.Documents;

namespace Sensus.Infrastructure.Persistence.Mongo.Repositories;

/// <summary>
/// Implementación de IOfferRepository con persistencia directa en MongoDB Atlas y actualización masiva de expiración (SE-022 a SE-026).
/// </summary>
public sealed class MongoOfferRepository : IOfferRepository
{
    private readonly MongoDbContext _context;
    private readonly ILogger<MongoOfferRepository> _logger;

    public MongoOfferRepository(
        MongoDbContext context,
        ILogger<MongoOfferRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<Offer?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var filter = Builders<OfferDocument>.Filter.Eq(o => o.Id, id);
        var document = await _context.Offers.Find(filter).FirstOrDefaultAsync(cancellationToken);
        return document?.ToDomain();
    }

    public async Task<IReadOnlyList<Offer>> GetByMerchantIdAsync(Guid merchantId, CancellationToken cancellationToken = default)
    {
        var filter = Builders<OfferDocument>.Filter.Eq(o => o.MerchantId, merchantId);
        var sort = Builders<OfferDocument>.Sort.Descending(o => o.CreatedAtUtc);
        var documents = await _context.Offers.Find(filter).Sort(sort).ToListAsync(cancellationToken);
        return documents.Select(d => d.ToDomain()).ToList();
    }

    public async Task AddAsync(Offer offer, CancellationToken cancellationToken = default)
    {
        var document = OfferDocument.FromDomain(offer);
        await _context.Offers.InsertOneAsync(document, cancellationToken: cancellationToken);
    }

    public async Task UpdateAsync(Offer offer, CancellationToken cancellationToken = default)
    {
        var document = OfferDocument.FromDomain(offer);
        var filter = Builders<OfferDocument>.Filter.Eq(o => o.Id, offer.Id);
        await _context.Offers.ReplaceOneAsync(filter, document, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Transición masiva y atómica en MongoDB para expirar ofertas cuya vigencia finalizó.
    /// Reemplaza la necesidad de Triggers o SQL Agent Jobs aprovechando el índice compuesto (Status, EndsAt).
    /// </summary>
    public async Task<int> ExpireOffersAsync(DateTime nowUtc, CancellationToken cancellationToken = default)
    {
        var filter = Builders<OfferDocument>.Filter.And(
            Builders<OfferDocument>.Filter.In(o => o.Status, [OfferStatus.Published.ToString(), OfferStatus.Paused.ToString()]),
            Builders<OfferDocument>.Filter.Lte(o => o.EndsAt, nowUtc)
        );

        var update = Builders<OfferDocument>.Update
            .Set(o => o.Status, OfferStatus.Expired.ToString())
            .Set(o => o.LastModifiedAtUtc, nowUtc);

        var result = await _context.Offers.UpdateManyAsync(filter, update, cancellationToken: cancellationToken);
        _logger.LogInformation("Actualización masiva de vigencia en MongoDB: {ModifiedCount} ofertas expiradas a las {NowUtc}.",
            result.ModifiedCount, nowUtc);

        return (int)result.ModifiedCount;
    }
}
