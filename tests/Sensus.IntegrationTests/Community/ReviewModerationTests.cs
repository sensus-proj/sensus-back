using NSubstitute;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Common.Moderation;

namespace Sensus.IntegrationTests.Community;

public sealed class ReviewModerationTests
{
    [Theory]
    [InlineData("Approved", true)]
    [InlineData("Rejected", false)]
    [InlineData("Quarantine", false)]
    public async Task ReviewVerdict_IsRecordedBeforePublicationCanProceed(string verdict, bool approved)
    {
        var client = Substitute.For<IPublicationTextModerationService>();
        var evidence = Substitute.For<IModerationEvidenceStore>();
        var clock = Substitute.For<IDateTimeProvider>();
        var contentId = Guid.NewGuid();
        const string comment = "Muy buena atención en el comercio.";
        client.ModerateTextAsync(comment, Arg.Any<CancellationToken>())
            .Returns(new ImageModerationResult(approved, Decision: verdict, AppliedThresholdSummary: "Sensus:gpt-4o-mini"));
        var moderation = new ReviewTextModeration(client, evidence, clock);

        var result = await moderation.EnsureApprovedAsync(contentId, "StoreReview", comment, CancellationToken.None);

        Assert.Equal(approved, result.IsSuccess);
        await evidence.Received(1).AppendAsync(
            Arg.Is<ModerationEvidence>(entry => entry.ContentId == contentId && entry.Decision == verdict
                && entry.Threshold == "Sensus:gpt-4o-mini" && entry.Action == (approved ? "Allow" : "Block")),
            CancellationToken.None);
    }

    [Fact]
    public async Task MissingProviderVerdict_CannotApproveReview()
    {
        var client = Substitute.For<IPublicationTextModerationService>();
        var evidence = Substitute.For<IModerationEvidenceStore>();
        client.ModerateTextAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ImageModerationResult>(null!));
        var moderation = new ReviewTextModeration(client, evidence, Substitute.For<IDateTimeProvider>());

        var result = await moderation.EnsureApprovedAsync(
            Guid.NewGuid(), "ReviewReply", "Gracias por compartir tu experiencia.", CancellationToken.None);

        Assert.True(result.IsFailure);
        await evidence.Received(1).AppendAsync(
            Arg.Is<ModerationEvidence>(entry => entry.Decision == "Quarantine" && entry.Action == "Block"),
            CancellationToken.None);
    }
}
