using MongoDB.Bson;
using MongoDB.Driver;
using Sensus.Application.Common.Interfaces;
using Sensus.Domain.Enums;
using Sensus.Infrastructure.Persistence.Mongo.Documents;

namespace Sensus.Infrastructure.Persistence.Mongo.Repositories;

/// <summary>
/// Resuelve existencia/autoría y marca contenido en revisión automática en MongoDB (Spec 040).
/// </summary>
public sealed class MongoReportTargetService : IReportTargetService
{
    private readonly MongoDbContext _context;

    public MongoReportTargetService(MongoDbContext context)
    {
        _context = context;
    }

    public async Task<ReportTargetInfo> GetTargetInfoAsync(
        ReportTargetType targetType,
        Guid targetId,
        CancellationToken cancellationToken = default)
    {
        switch (targetType)
        {
            case ReportTargetType.Offer:
                {
                    OfferDocument? offer = await _context.Offers
                        .Find(o => o.Id == targetId)
                        .FirstOrDefaultAsync(cancellationToken);

                    if (offer is null)
                    {
                        return new ReportTargetInfo(Exists: false, AuthorUserId: null);
                    }

                    ComercioDocument? comercio = await _context.Comercios
                        .Find(c => c.Id == offer.MerchantId)
                        .FirstOrDefaultAsync(cancellationToken);

                    return new ReportTargetInfo(Exists: true, AuthorUserId: comercio?.UsuarioId);
                }
            case ReportTargetType.Post:
                {
                    BsonDocument? post = await _context.FeedPostsRaw
                        .Find(Builders<BsonDocument>.Filter.Eq("_id", targetId.ToString()))
                        .FirstOrDefaultAsync(cancellationToken);

                    if (post is null)
                    {
                        return new ReportTargetInfo(Exists: false, AuthorUserId: null);
                    }

                    bool isDeleted = post.TryGetValue("isDeleted", out BsonValue deletedVal) && deletedVal.IsBoolean && deletedVal.AsBoolean;
                    if (isDeleted)
                    {
                        return new ReportTargetInfo(Exists: false, AuthorUserId: null);
                    }

                    Guid? authorId = MongoFeedPostVisibilityLookup.TryReadGuid(post, "authorUserId")
                                     ?? MongoFeedPostVisibilityLookup.TryReadGuid(post, "userId");
                    return new ReportTargetInfo(Exists: true, AuthorUserId: authorId);
                }
            case ReportTargetType.Comment:
                {
                    FeedCommentDocument? comment = await _context.FeedComments
                        .Find(c => c.Id == targetId)
                        .FirstOrDefaultAsync(cancellationToken);

                    return comment is null
                        ? new ReportTargetInfo(Exists: false, AuthorUserId: null)
                        : new ReportTargetInfo(Exists: true, AuthorUserId: comment.AuthorUserId);
                }
            default:
                return new ReportTargetInfo(Exists: false, AuthorUserId: null);
        }
    }

    public async Task<bool> MarkUnderReviewAsync(
        ReportTargetType targetType,
        Guid targetId,
        DateTime flaggedAtUtc,
        CancellationToken cancellationToken = default)
    {
        UpdateDefinition<BsonDocument> update = Builders<BsonDocument>.Update
            .Set("underReview", true)
            .Set("flaggedAtUtc", flaggedAtUtc);

        FilterDefinition<BsonDocument> filter = Builders<BsonDocument>.Filter.Eq("_id", targetId.ToString());

        IMongoCollection<BsonDocument> collection = targetType switch
        {
            ReportTargetType.Offer => _context.OffersRaw,
            ReportTargetType.Post => _context.FeedPostsRaw,
            ReportTargetType.Comment => _context.FeedCommentsRaw,
            _ => _context.FeedCommentsRaw
        };

        UpdateResult result = await collection.UpdateOneAsync(filter, update, cancellationToken: cancellationToken);
        return result.MatchedCount > 0;
    }
}
