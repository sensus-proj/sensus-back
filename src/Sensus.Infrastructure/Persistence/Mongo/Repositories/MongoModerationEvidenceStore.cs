using MongoDB.Bson;
using MongoDB.Driver;
using Sensus.Application.Common.Moderation;

namespace Sensus.Infrastructure.Persistence.Mongo.Repositories;

public sealed class MongoModerationEvidenceStore(MongoDbContext context, Func<string> correlationId)
    : IModerationEvidenceStore
{
    public Task AppendAsync(ModerationEvidence evidence, CancellationToken cancellationToken) =>
        context.ModerationDecisions.InsertOneAsync(new BsonDocument
        {
            { "contentId", evidence.ContentId.ToString() },
            { "contentType", evidence.ContentType },
            { "decision", evidence.Decision },
            { "categories", new BsonArray(evidence.Categories ?? []) },
            { "scores", new BsonArray((evidence.Scores ?? new Dictionary<string, double>())
                .Select(score => new BsonDocument { { "category", score.Key }, { "score", score.Value } })) },
            { "threshold", evidence.Threshold is null ? BsonNull.Value : new BsonString(evidence.Threshold) },
            { "action", evidence.Action },
            { "evaluatedAtUtc", evidence.EvaluatedAtUtc },
            { "correlationId", correlationId() }
        }, cancellationToken: cancellationToken);
}
