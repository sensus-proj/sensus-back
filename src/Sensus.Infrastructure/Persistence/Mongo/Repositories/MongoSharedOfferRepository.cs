using MongoDB.Driver;
using Sensus.Domain.Entities;
using Sensus.Domain.Repositories;
using Sensus.Infrastructure.Persistence.Mongo.Documents;

namespace Sensus.Infrastructure.Persistence.Mongo.Repositories;

/// <summary>
/// Implementación en MongoDB Atlas para ofertas compartidas entre amigos.
/// </summary>
public sealed class MongoSharedOfferRepository : ISharedOfferRepository
{
    private readonly MongoDbContext _context;

    public MongoSharedOfferRepository(MongoDbContext context)
    {
        _context = context;
    }

    public async Task AddRangeAsync(IReadOnlyCollection<SharedOffer> items, CancellationToken cancellationToken = default)
    {
        if (items.Count == 0)
        {
            return;
        }

        List<SharedOfferDocument> documents = items.Select(SharedOfferDocument.FromDomain).ToList();
        await _context.SharedOffers.InsertManyAsync(documents, cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyList<SharedOffer>> GetConversationAsync(
        Guid userA,
        Guid userB,
        CancellationToken cancellationToken = default)
    {
        FilterDefinition<SharedOfferDocument> direct = Builders<SharedOfferDocument>.Filter.And(
            Builders<SharedOfferDocument>.Filter.Eq(s => s.SenderUserId, userA),
            Builders<SharedOfferDocument>.Filter.Eq(s => s.RecipientUserId, userB));

        FilterDefinition<SharedOfferDocument> reverse = Builders<SharedOfferDocument>.Filter.And(
            Builders<SharedOfferDocument>.Filter.Eq(s => s.SenderUserId, userB),
            Builders<SharedOfferDocument>.Filter.Eq(s => s.RecipientUserId, userA));

        FilterDefinition<SharedOfferDocument> filter = Builders<SharedOfferDocument>.Filter.Or(direct, reverse);
        List<SharedOfferDocument> documents = await _context.SharedOffers
            .Find(filter)
            .SortBy(s => s.SharedAtUtc)
            .Limit(200)
            .ToListAsync(cancellationToken);

        return documents.Select(d => d.ToDomain()).ToList();
    }
}
