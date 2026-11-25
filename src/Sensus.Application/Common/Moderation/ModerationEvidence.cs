using Sensus.Application.Common.Interfaces;
using Sensus.Domain.Common;

namespace Sensus.Application.Common.Moderation;

public sealed record ModerationEvidence(
    Guid ContentId,
    string ContentType,
    string Decision,
    IReadOnlyList<string>? Categories,
    IReadOnlyDictionary<string, double>? Scores,
    string? Threshold,
    string Action,
    DateTime EvaluatedAtUtc);

public interface IModerationEvidenceStore
{
    Task AppendAsync(ModerationEvidence evidence, CancellationToken cancellationToken);
}

public sealed class ReviewTextModeration(
    IPublicationTextModerationService moderator,
    IModerationEvidenceStore evidence,
    IDateTimeProvider clock)
{
    public async Task<Result> EnsureApprovedAsync(
        Guid contentId, string contentType, string text, CancellationToken cancellationToken)
    {
        var decision = ProfanityFilter.ContainsProfanity(text)
            ? new ImageModerationResult(false, Decision: "Rejected", AppliedThresholdSummary: "profanity-filter")
            : await moderator.ModerateTextAsync(text, cancellationToken);
        bool approved = decision is { IsApproved: true, Decision: "Approved" };
        string verdict = approved ? "Approved" : decision is { IsApproved: false, Decision: "Rejected" } ? "Rejected" : "Quarantine";
        await evidence.AppendAsync(new ModerationEvidence(
            contentId, contentType, verdict, decision?.FlaggedCategories, decision?.CategoryScores,
            decision?.AppliedThresholdSummary, approved ? "Allow" : "Block", clock.UtcNow), cancellationToken);
        return approved
            ? Result.Success()
            : Result.Failure(Error.Validation("StoreReview.TextNotApproved", "No se pudo aprobar el texto para publicarlo."));
    }
}
