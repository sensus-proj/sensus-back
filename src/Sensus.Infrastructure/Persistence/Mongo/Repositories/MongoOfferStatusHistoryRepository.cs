using MongoDB.Driver;
using Sensus.Domain.Entities;
using Sensus.Domain.Repositories;
using Sensus.Infrastructure.Persistence.Mongo.Documents;

namespace Sensus.Infrastructure.Persistence.Mongo.Repositories;

/// <summary>
/// Implementación MongoDB del historial de transiciones de estado de ofertas (Spec 036).
/// </summary>
public sealed class MongoOfferStatusHistoryRepository : IOfferStatusHistoryRepository
{
    private readonly MongoDbContext _context;

    public MongoOfferStatusHistoryRepository(MongoDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(OfferStatusTransition transition, CancellationToken cancellationToken = default)
    {
        await _context.OfferStatusHistory.InsertOneAsync(
            OfferStatusHistoryDocument.FromDomain(transition),
            cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyList<OfferStatusTransition>> GetByOfferIdAsync(
        Guid offerId,
        CancellationToken cancellationToken = default)
    {
        List<OfferStatusHistoryDocument> docs = await _context.OfferStatusHistory
            .Find(h => h.OfferId == offerId)
            .SortBy(h => h.OccurredAtUtc)
            .ToListAsync(cancellationToken);

        return docs.Select(d => d.ToDomain()).ToList();
    }
}
