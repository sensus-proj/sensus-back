using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using Sensus.Domain.Entities;
using Sensus.Domain.Enums;
using Sensus.Domain.Models;
using Sensus.Domain.Repositories;
using Sensus.Infrastructure.Persistence.Mongo.Documents;

namespace Sensus.Infrastructure.Persistence.Mongo.Repositories;

/// <summary>
/// Implementación MongoDB de los apartados de oferta con canje atómico (FR-033-01 a FR-033-08).
/// </summary>
public sealed class MongoOfferClaimRepository : IOfferClaimRepository
{
    private readonly MongoDbContext _context;
    private readonly ILogger<MongoOfferClaimRepository> _logger;

    public MongoOfferClaimRepository(
        MongoDbContext context,
        ILogger<MongoOfferClaimRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<OfferClaim?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        OfferClaimDocument? document = await _context.OfferClaims
            .Find(Builders<OfferClaimDocument>.Filter.Eq(c => c.Id, id))
            .FirstOrDefaultAsync(cancellationToken);

        return document?.ToDomain();
    }

    public async Task<int> GetActiveClaimCountAsync(Guid offerId, CancellationToken cancellationToken = default)
    {
        var filterBuilder = Builders<OfferClaimDocument>.Filter;
        var offerMatch = filterBuilder.Or(
            filterBuilder.Eq(c => c.OfferId, offerId),
            filterBuilder.AnyEq("offerIds", offerId));
        var filter = filterBuilder.And(
            offerMatch,
            filterBuilder.Eq(c => c.Status, OfferClaimStatus.Active.ToString()));

        return (int)await _context.OfferClaims.CountDocumentsAsync(filter, cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyDictionary<Guid, OfferClaimCounts>> CountByOfferIdsAsync(
        IEnumerable<Guid> offerIds,
        CancellationToken cancellationToken = default)
    {
        List<Guid> ids = offerIds.Where(id => id != Guid.Empty).Distinct().ToList();
        if (ids.Count == 0)
        {
            return new Dictionary<Guid, OfferClaimCounts>();
        }

        // FR-037-03 y NFR-037-02: una sola consulta con IN sobre el lote de la página; nunca una
        // consulta por oferta. La proyección evita traer hashes de código ni datos de usuario.
        var filterBuilder = Builders<OfferClaimDocument>.Filter;
        var filter = filterBuilder.Or(
            filterBuilder.In(c => c.OfferId, ids),
            filterBuilder.AnyIn("offerIds", ids));
        var projection = Builders<OfferClaimDocument>.Projection
            .Include(c => c.OfferId)
            .Include(c => c.OfferIds)
            .Include(c => c.Status);

        List<OfferClaimCountProjection> claims = await _context.OfferClaims
            .Find(filter)
            .Project<OfferClaimCountProjection>(projection)
            .ToListAsync(cancellationToken);

        var idSet = ids.ToHashSet();
        var counts = new Dictionary<Guid, (int Active, int Redeemed)>();
        foreach (OfferClaimCountProjection claim in claims)
        {
            bool isActive = string.Equals(claim.Status, OfferClaimStatus.Active.ToString(), StringComparison.OrdinalIgnoreCase);
            bool isRedeemed = string.Equals(claim.Status, OfferClaimStatus.Redeemed.ToString(), StringComparison.OrdinalIgnoreCase);
            if (!isActive && !isRedeemed)
            {
                continue;
            }

            IEnumerable<Guid> coveredIds = claim.OfferIds is { Count: > 0 }
                ? claim.OfferIds
                : [claim.OfferId];

            foreach (Guid coveredId in coveredIds.Where(idSet.Contains).Distinct())
            {
                counts.TryGetValue(coveredId, out var current);
                counts[coveredId] = (
                    current.Active + (isActive ? 1 : 0),
                    current.Redeemed + (isRedeemed ? 1 : 0));
            }
        }

        return counts.ToDictionary(
            kvp => kvp.Key,
            kvp => new OfferClaimCounts(kvp.Value.Active, kvp.Value.Redeemed));
    }

    public async Task<OfferClaim?> GetActiveByUserAndOfferAsync(
        Guid userId,
        Guid offerId,
        CancellationToken cancellationToken = default)
    {
        var filterBuilder = Builders<OfferClaimDocument>.Filter;
        var filter = filterBuilder.And(
            filterBuilder.Eq(c => c.UserId, userId),
            filterBuilder.Or(
                filterBuilder.Eq(c => c.OfferId, offerId),
                filterBuilder.AnyEq("offerIds", offerId)),
            filterBuilder.Eq(c => c.Status, OfferClaimStatus.Active.ToString()));

        OfferClaimDocument? document = await _context.OfferClaims
            .Find(filter)
            .FirstOrDefaultAsync(cancellationToken);

        return document?.ToDomain();
    }

    public async Task<OfferClaim?> GetActiveByUserAndAnyOfferAsync(
        Guid userId,
        IEnumerable<Guid> offerIds,
        CancellationToken cancellationToken = default)
    {
        List<Guid> ids = offerIds.Where(id => id != Guid.Empty).Distinct().ToList();
        if (ids.Count == 0)
        {
            return null;
        }

        var filterBuilder = Builders<OfferClaimDocument>.Filter;
        var filter = filterBuilder.And(
            filterBuilder.Eq(c => c.UserId, userId),
            filterBuilder.Or(
                filterBuilder.In(c => c.OfferId, ids),
                filterBuilder.AnyIn("offerIds", ids)),
            filterBuilder.Eq(c => c.Status, OfferClaimStatus.Active.ToString()));

        OfferClaimDocument? document = await _context.OfferClaims
            .Find(filter)
            .FirstOrDefaultAsync(cancellationToken);

        return document?.ToDomain();
    }

    public async Task<OfferClaim?> GetByCodeHashAsync(string codeHash, CancellationToken cancellationToken = default)
    {
        OfferClaimDocument? document = await _context.OfferClaims
            .Find(Builders<OfferClaimDocument>.Filter.Eq(c => c.CodeHash, codeHash))
            .FirstOrDefaultAsync(cancellationToken);

        return document?.ToDomain();
    }

    public async Task<bool> TryAddAsync(OfferClaim claim, CancellationToken cancellationToken = default)
    {
        try
        {
            await _context.OfferClaims.InsertOneAsync(
                OfferClaimDocument.FromDomain(claim),
                cancellationToken: cancellationToken);
            return true;
        }
        catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            _logger.LogInformation(
                "Apartado duplicado rechazado para la oferta {OfferId} por el índice único activo.",
                claim.OfferId);
            return false;
        }
    }

    public async Task UpdateAsync(OfferClaim claim, CancellationToken cancellationToken = default)
    {
        var filter = Builders<OfferClaimDocument>.Filter.Eq(c => c.Id, claim.Id);
        await _context.OfferClaims.ReplaceOneAsync(
            filter,
            OfferClaimDocument.FromDomain(claim),
            cancellationToken: cancellationToken);
    }

    public async Task<bool> TryRedeemAsync(
        Guid claimId,
        Guid merchantId,
        DateTime nowUtc,
        CancellationToken cancellationToken = default)
    {
        var filter = Builders<OfferClaimDocument>.Filter.And(
            Builders<OfferClaimDocument>.Filter.Eq(c => c.Id, claimId),
            Builders<OfferClaimDocument>.Filter.Eq(c => c.MerchantId, merchantId),
            Builders<OfferClaimDocument>.Filter.Eq(c => c.Status, OfferClaimStatus.Active.ToString()),
            Builders<OfferClaimDocument>.Filter.Gt(c => c.ExpiresAtUtc, nowUtc));

        var update = Builders<OfferClaimDocument>.Update
            .Set(c => c.Status, OfferClaimStatus.Redeemed.ToString())
            .Set(c => c.RedeemedAtUtc, nowUtc);

        OfferClaimDocument? redeemed = await _context.OfferClaims.FindOneAndUpdateAsync(
            filter,
            update,
            cancellationToken: cancellationToken);

        return redeemed is not null;
    }

    public async Task<IReadOnlyList<OfferClaim>> ExpireClaimsAsync(
        DateTime nowUtc,
        CancellationToken cancellationToken = default)
    {
        var filter = Builders<OfferClaimDocument>.Filter.And(
            Builders<OfferClaimDocument>.Filter.Eq(c => c.Status, OfferClaimStatus.Active.ToString()),
            Builders<OfferClaimDocument>.Filter.Lte(c => c.ExpiresAtUtc, nowUtc));

        List<OfferClaimDocument> candidates = await _context.OfferClaims
            .Find(filter)
            .ToListAsync(cancellationToken);

        var expired = new List<OfferClaim>(candidates.Count);

        foreach (OfferClaimDocument candidate in candidates)
        {
            var transitionFilter = Builders<OfferClaimDocument>.Filter.And(
                Builders<OfferClaimDocument>.Filter.Eq(c => c.Id, candidate.Id),
                Builders<OfferClaimDocument>.Filter.Eq(c => c.Status, OfferClaimStatus.Active.ToString()));

            var update = Builders<OfferClaimDocument>.Update
                .Set(c => c.Status, OfferClaimStatus.Expired.ToString());

            OfferClaimDocument? transitioned = await _context.OfferClaims.FindOneAndUpdateAsync(
                transitionFilter,
                update,
                new FindOneAndUpdateOptions<OfferClaimDocument> { ReturnDocument = ReturnDocument.After },
                cancellationToken);

            if (transitioned is not null)
            {
                expired.Add(transitioned.ToDomain());
            }
        }

        return expired;
    }

    public async Task<IReadOnlyList<OfferClaim>> GetByUserIdAsync(
        Guid userId,
        OfferClaimStatus? status = null,
        int limit = 10,
        CancellationToken cancellationToken = default)
    {
        var filterBuilder = Builders<OfferClaimDocument>.Filter;
        var filter = filterBuilder.Eq(c => c.UserId, userId);

        if (status.HasValue)
        {
            filter = filterBuilder.And(filter, filterBuilder.Eq(c => c.Status, status.Value.ToString()));
        }

        var sort = Builders<OfferClaimDocument>.Sort.Descending(c => c.CreatedAtUtc);
        var safeLimit = Math.Clamp(limit, 1, 50);

        var documents = await _context.OfferClaims
            .Find(filter)
            .Sort(sort)
            .Limit(safeLimit)
            .ToListAsync(cancellationToken);

        return documents.Select(d => d.ToDomain()).ToList();
    }

    public async Task<int> ExpireActiveByMerchantAsync(
        Guid merchantId,
        DateTime nowUtc,
        CancellationToken cancellationToken = default)
    {
        FilterDefinition<OfferClaimDocument> filter = Builders<OfferClaimDocument>.Filter.And(
            Builders<OfferClaimDocument>.Filter.Eq(c => c.MerchantId, merchantId),
            Builders<OfferClaimDocument>.Filter.Eq(c => c.Status, OfferClaimStatus.Active.ToString()));

        UpdateDefinition<OfferClaimDocument> update = Builders<OfferClaimDocument>.Update
            .Set(c => c.Status, OfferClaimStatus.Expired.ToString())
            .Set(c => c.ExpiresAtUtc, DateTime.SpecifyKind(nowUtc, DateTimeKind.Utc));

        var result = await _context.OfferClaims.UpdateManyAsync(filter, update, cancellationToken: cancellationToken);
        return (int)Math.Min(result.ModifiedCount, int.MaxValue);
    }

    /// <summary>Proyección mínima para el conteo batch; excluye hash, usuario y fechas.</summary>
    private sealed class OfferClaimCountProjection
    {
        [BsonElement("offerId")]
        [BsonRepresentation(BsonType.String)]
        public Guid OfferId { get; set; }

        [BsonElement("offerIds")]
        [BsonRepresentation(BsonType.String)]
        [BsonIgnoreIfNull]
        public List<Guid>? OfferIds { get; set; }

        [BsonElement("status")]
        public string Status { get; set; } = string.Empty;
    }
}
