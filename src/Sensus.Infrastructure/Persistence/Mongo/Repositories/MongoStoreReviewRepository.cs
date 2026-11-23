using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using Sensus.Domain.Entities;
using Sensus.Domain.Repositories;
using Sensus.Infrastructure.Persistence.Mongo.Documents;

namespace Sensus.Infrastructure.Persistence.Mongo.Repositories;

public sealed class MongoStoreReviewRepository(MongoDbContext context) : IStoreReviewRepository
{
    public async Task<StoreReview?> GetByIdAsync(Guid reviewId, CancellationToken cancellationToken = default)
    {
        var document = await context.StoreReviews.Find(review => review.Id == reviewId)
            .FirstOrDefaultAsync(cancellationToken);
        return document?.ToDomain();
    }

    public async Task<(IReadOnlyList<StoreReview> Reviews, IReadOnlyDictionary<int, int> Ratings)> GetMerchantPageAsync(
        Guid merchantId, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        BsonDocument[] pipeline =
        [
            new("$match", new BsonDocument("merchantId", merchantId.ToString())),
            new("$facet", new BsonDocument
            {
                { "ratings", new BsonArray
                    {
                        new BsonDocument("$group", new BsonDocument { { "_id", "$rating" }, { "count", new BsonDocument("$sum", 1) } })
                    }
                },
                { "reviews", new BsonArray
                    {
                        new BsonDocument("$sort", new BsonDocument { { "createdAtUtc", -1 }, { "_id", 1 } }),
                        new BsonDocument("$skip", (page - 1L) * pageSize),
                        new BsonDocument("$limit", pageSize)
                    }
                }
            })
        ];
        var result = await context.StoreReviews.Aggregate<BsonDocument>(pipeline, cancellationToken: cancellationToken)
            .FirstOrDefaultAsync(cancellationToken);
        if (result is null)
        {
            return ([], new Dictionary<int, int>());
        }

        return (
            result["reviews"].AsBsonArray.Select(review =>
                BsonSerializer.Deserialize<StoreReviewDocument>(review.AsBsonDocument).ToDomain()).ToList(),
            result["ratings"].AsBsonArray.ToDictionary(rating => rating["_id"].ToInt32(), rating => rating["count"].ToInt32()));
    }

    public Task AddAsync(StoreReview review, CancellationToken cancellationToken = default) =>
        context.StoreReviews.InsertOneAsync(StoreReviewDocument.FromDomain(review), cancellationToken: cancellationToken);

    public async Task<bool> AddReplyAsync(Guid reviewId, StoreReviewReply reply, CancellationToken cancellationToken = default)
    {
        var result = await context.StoreReviews.UpdateOneAsync(review => review.Id == reviewId,
            Builders<StoreReviewDocument>.Update.Push(review => review.Replies, StoreReviewReplySubDocument.FromDomain(reply)),
            cancellationToken: cancellationToken);
        return result.MatchedCount == 1;
    }

    public async Task<(bool IsLiked, int Count)?> ToggleLikeAsync(
        Guid reviewId, Guid userId, CancellationToken cancellationToken = default)
    {
        var user = userId.ToString();
        var likes = new BsonDocument("$ifNull", new BsonArray { "$likedByUserIds", new BsonArray() });
        BsonDocument[] update =
        [
            new("$set", new BsonDocument("likedByUserIds", new BsonDocument("$cond", new BsonArray
            {
                new BsonDocument("$in", new BsonArray { user, likes }),
                new BsonDocument("$setDifference", new BsonArray { likes, new BsonArray { user } }),
                new BsonDocument("$setUnion", new BsonArray { likes, new BsonArray { user } })
            })))
        ];
        var result = await context.StoreReviews.FindOneAndUpdateAsync(review => review.Id == reviewId,
            Builders<StoreReviewDocument>.Update.Pipeline(update),
            new FindOneAndUpdateOptions<StoreReviewDocument> { ReturnDocument = ReturnDocument.After }, cancellationToken);
        return result is null ? null : (result.LikedByUserIds.Contains(userId), result.LikedByUserIds.Count);
    }
}
