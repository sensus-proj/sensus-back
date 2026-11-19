using MongoDB.Driver;
using Sensus.Application.Offers.Notifications;
using Sensus.Infrastructure.Persistence.Mongo.Documents;

namespace Sensus.Infrastructure.Persistence.Mongo.Repositories;

public sealed class MongoClaimPushReceiptStore(MongoDbContext context) : IClaimPushReceiptStore
{
    public async Task<bool> TryReserveAsync(
        Guid claimId, Guid deviceId, DateTime expiresAtUtc, CancellationToken cancellationToken)
    {
        var receipt = new ClaimPushReceiptDocument
        {
            Id = $"{claimId:N}:{deviceId:N}",
            ExpiresAtUtc = expiresAtUtc
        };

        try
        {
            await context.ClaimPushReceipts.InsertOneAsync(receipt, cancellationToken: cancellationToken);
            return true;
        }
        catch (MongoWriteException exception) when (exception.WriteError.Category == ServerErrorCategory.DuplicateKey)
        {
            return false;
        }
    }
}
