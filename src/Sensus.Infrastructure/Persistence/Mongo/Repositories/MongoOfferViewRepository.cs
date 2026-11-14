using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using Sensus.Domain.Entities;
using Sensus.Domain.Repositories;
using Sensus.Infrastructure.Persistence.Mongo.Documents;

namespace Sensus.Infrastructure.Persistence.Mongo.Repositories;

/// <summary>
/// Implementación MongoDB de las vistas de oferta con deduplicación respaldada por índice único
/// (FR-032-03, FR-032-04 y NFR-032-02).
/// </summary>
public sealed class MongoOfferViewRepository : IOfferViewRepository
{
    private readonly MongoDbContext _context;
    private readonly ILogger<MongoOfferViewRepository> _logger;

    public MongoOfferViewRepository(
        MongoDbContext context,
        ILogger<MongoOfferViewRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<bool> TryRegisterAsync(OfferView view, CancellationToken cancellationToken = default)
    {
        try
        {
            await _context.OfferViews.InsertOneAsync(
                OfferViewDocument.FromDomain(view),
                cancellationToken: cancellationToken);
            return true;
        }
        catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            _logger.LogDebug(
                "Vista duplicada del día ignorada para la oferta {OfferId}.",
                view.OfferId);
            return false;
        }
    }
}
