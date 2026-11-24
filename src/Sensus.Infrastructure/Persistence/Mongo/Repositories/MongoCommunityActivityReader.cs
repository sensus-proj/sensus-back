using MongoDB.Bson;
using MongoDB.Driver;
using Sensus.Application.Common.Models;
using Sensus.Application.Friends.DTOs;
using Sensus.Application.Friends.Queries.GetFriendsActivity;

namespace Sensus.Infrastructure.Persistence.Mongo.Repositories;

public sealed class MongoCommunityActivityReader(MongoDbContext context) : ICommunityActivityReader
{
    public async Task<PagedResult<CommunityActivityItemDto>> ReadAsync(
        IReadOnlyCollection<Guid> visibleActorIds, int page, int pageSize, CancellationToken cancellationToken)
    {
        var actors = new BsonArray(visibleActorIds.Select(id => id.ToString()));
        var reviews = context.StoreReviews.CollectionNamespace.CollectionName;
        BsonDocument[] pipeline =
        [
            MatchAuthors("userId", actors),
            Lookup(context.Offers.CollectionNamespace.CollectionName, "offerId", "offer"),
            new("$unwind", "$offer"),
            new("$match", new BsonDocument("offer.status", new BsonDocument("$in", new BsonArray { "Published", "Paused", "Expired" }))),
            ProjectEvent("OfferLike", "offer-like:", "$_id", "$userId", "$offer.merchantId", "$createdAtUtc",
                "$offerId", "$offer.title"),
            Union(reviews, [
                MatchAuthors("authorUserId", actors),
                ProjectEvent("StoreReview", "review:", "$_id", "$authorUserId", "$merchantId", "$createdAtUtc",
                    comment: "$comment", rating: "$rating")
            ]),
            Union(reviews, [
                MatchAuthors("replies.authorUserId", actors),
                new("$unwind", "$replies"),
                MatchAuthors("replies.authorUserId", actors),
                ProjectEvent("ReviewReply", "reply:", "$replies.id", "$replies.authorUserId", "$merchantId",
                    "$replies.createdAtUtc", comment: "$replies.comment")
            ]),
            Lookup(context.Comercios.CollectionNamespace.CollectionName, "merchantId", "merchant"),
            new("$unwind", "$merchant"),
            new("$match", new BsonDocument("merchant.activo", true)),
            new("$facet", new BsonDocument
            {
                { "total", new BsonArray { new BsonDocument("$count", "count") } },
                { "items", new BsonArray
                    {
                        new BsonDocument("$sort", new BsonDocument { { "createdAtUtc", -1 }, { "id", 1 } }),
                        new BsonDocument("$skip", (page - 1L) * pageSize),
                        new BsonDocument("$limit", pageSize),
                        Lookup(context.Users.CollectionNamespace.CollectionName, "actorUserId", "actor")
                    }
                }
            })
        ];

        var result = await context.OfferLikes.Aggregate<BsonDocument>(pipeline, cancellationToken: cancellationToken)
            .FirstOrDefaultAsync(cancellationToken);
        if (result is null)
        {
            return new PagedResult<CommunityActivityItemDto>([], 0, page, pageSize);
        }

        var totals = result["total"].AsBsonArray;
        var total = totals.Count == 0 ? 0 : totals[0]["count"].ToInt32();
        return new PagedResult<CommunityActivityItemDto>(
            result["items"].AsBsonArray.Select(item => Map(item.AsBsonDocument)).ToList(), total, page, pageSize);
    }

    private static BsonDocument MatchAuthors(string field, BsonArray actors) =>
        new("$match", new BsonDocument(field, new BsonDocument("$in", actors)));

    private static BsonDocument Lookup(string collection, string localField, string alias) =>
        new("$lookup", new BsonDocument
        {
            { "from", collection },
            { "localField", localField },
            { "foreignField", "_id" },
            { "as", alias }
        });

    private static BsonDocument Union(string collection, BsonDocument[] pipeline) =>
        new("$unionWith", new BsonDocument { { "coll", collection }, { "pipeline", new BsonArray(pipeline) } });

    private static BsonDocument ProjectEvent(
        string kind, string prefix, string id, string author, string merchant, string time,
        string? offerId = null, string? title = null, string? comment = null, string? rating = null) =>
        new("$project", new BsonDocument
        {
            { "_id", 0 },
            { "id", new BsonDocument("$concat", new BsonArray { prefix, id }) },
            { "kind", new BsonDocument("$literal", kind) },
            { "actorUserId", author },
            { "merchantId", merchant },
            { "createdAtUtc", time },
            { "offerId", ExpressionOrNull(offerId) },
            { "title", ExpressionOrNull(title) },
            { "comment", ExpressionOrNull(comment) },
            { "rating", ExpressionOrNull(rating) }
        });

    private static BsonValue ExpressionOrNull(string? expression) =>
        expression is null ? new BsonDocument("$literal", BsonNull.Value) : new BsonString(expression);

    private static CommunityActivityItemDto Map(BsonDocument row)
    {
        var actor = row["actor"].AsBsonArray.FirstOrDefault()?.AsBsonDocument;
        var merchantName = ReadString(row["merchant"].AsBsonDocument, "nombreComercial") ?? "Comercio local";
        var title = ReadString(row, "title");
        return new CommunityActivityItemDto(
            row["id"].AsString,
            row["kind"].AsString,
            Guid.Parse(row["actorUserId"].AsString),
            actor is null ? "Amigo Sensus" : ReadString(actor, "nombreCompleto") ?? "Amigo Sensus",
            actor is null ? null : ReadString(actor, "fotoPerfilUrl"),
            Guid.TryParse(ReadString(row, "offerId"), out var offerId) ? offerId : null,
            Guid.Parse(row["merchantId"].AsString),
            title ?? merchantName,
            title is null ? null : merchantName,
            ReadString(row, "comment"),
            row.TryGetValue("rating", out var rating) && rating.IsNumeric ? rating.ToInt32() : null,
            row["createdAtUtc"].ToUniversalTime());
    }

    private static string? ReadString(BsonDocument document, string key) =>
        document.TryGetValue(key, out var value) && value.IsString ? value.AsString : null;
}
