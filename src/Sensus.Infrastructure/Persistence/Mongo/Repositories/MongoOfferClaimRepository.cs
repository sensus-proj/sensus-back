using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using Sensus.Domain.Entities;
using Sensus.Domain.Enums;
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

    public async Task<OfferClaim?> GetActiveByUserAndOfferAsync(
        Guid userId,
        Guid offerId,
        CancellationToken cancellationToken = default)
    {
        var filter = Builders<OfferClaimDocument>.Filter.And(
            Builders<OfferClaimDocument>.Filter.Eq(c => c.UserId, userId),
            Builders<OfferClaimDocument>.Filter.Eq(c => c.OfferId, offerId),
            Builders<OfferClaimDocument>.Filter.Eq(c => c.Status, OfferClaimStatus.Active.ToString()));

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
}
