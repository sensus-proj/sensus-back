using FluentAssertions;
using Sensus.Domain.Common;
using Sensus.Domain.Entities;
using Sensus.Domain.Enums;
using Xunit;

namespace Sensus.Application.UnitTests.Moderation.Audit;

/// <summary>
/// Spec 021: invariantes del registro inmutable de auditoría de moderación.
/// Ref: FR-021-01 - veredicto, puntajes y umbral; FR-021-04 - retención; NFR-021-01 - solo adición.
/// </summary>
public sealed class ModerationAuditRecordTests
{
    private static readonly DateTime Now = new(2026, 11, 22, 10, 0, 0, DateTimeKind.Utc);

    private static Result<ModerationAuditRecord> CreateValid(
        Guid? targetId = null,
        int retentionDays = 30,
        IReadOnlyList<string>? categories = null,
        IReadOnlyDictionary<string, double>? scores = null) =>
        ModerationAuditRecord.Create(
            targetId ?? Guid.NewGuid(),
            ModerationTargetType.Offer,
            ModerationVerdict.Quarantined,
            "Quarantine",
            "offer_create",
            categories,
            scores,
            "hate>=0.70",
            ModerationActorType.User,
            "user-1",
            "corr-1",
            Now,
            retentionDays);

    [Fact]
    public void Create_WithValidData_PersistsEvidenceAndRetention()
    {
        Guid targetId = Guid.NewGuid();
        var scores = new Dictionary<string, double> { ["hate"] = 0.82 };

        Result<ModerationAuditRecord> result = CreateValid(targetId, 2555, ["hate"], scores);

        result.IsSuccess.Should().BeTrue();
        ModerationAuditRecord record = result.Value;
        record.Id.Should().NotBe(Guid.Empty);
        record.TargetId.Should().Be(targetId);
        record.TargetType.Should().Be(ModerationTargetType.Offer);
        record.Verdict.Should().Be(ModerationVerdict.Quarantined);
        record.Decision.Should().Be("Quarantine");
        record.ContentContext.Should().Be("offer_create");
        record.FlaggedCategories.Should().Equal("hate");
        record.CategoryScores["hate"].Should().Be(0.82);
        record.ThresholdApplied.Should().Be("hate>=0.70");
        record.ActorType.Should().Be(ModerationActorType.User);
        record.ActorId.Should().Be("user-1");
        record.CorrelationId.Should().Be("corr-1");
        record.CreatedAtUtc.Should().Be(Now);
        record.RetainUntilUtc.Should().Be(Now.AddDays(2555));
    }

    [Fact]
    public void Create_WithoutTarget_ReturnsValidationError()
    {
        Result<ModerationAuditRecord> result = CreateValid(Guid.Empty);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ModerationAudit.TargetRequired");
        result.Error.Type.Should().Be(ErrorType.Validation);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithoutDecision_ReturnsValidationError(string decision)
    {
        Result<ModerationAuditRecord> result = ModerationAuditRecord.Create(
            Guid.NewGuid(), ModerationTargetType.MediaAsset, ModerationVerdict.Approved, decision, "media_upload",
            null, null, null, ModerationActorType.System, "system", null, Now, 30);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ModerationAudit.DecisionRequired");
    }

    [Fact]
    public void Create_WithoutContext_ReturnsValidationError()
    {
        Result<ModerationAuditRecord> result = ModerationAuditRecord.Create(
            Guid.NewGuid(), ModerationTargetType.FeedComment, ModerationVerdict.Approved, "Approved", "  ",
            null, null, null, ModerationActorType.User, "user-1", null, Now, 30);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ModerationAudit.ContextRequired");
    }

    [Fact]
    public void Create_WithoutActor_ReturnsValidationError()
    {
        Result<ModerationAuditRecord> result = ModerationAuditRecord.Create(
            Guid.NewGuid(), ModerationTargetType.FeedComment, ModerationVerdict.Approved, "Approved", "feed_comment",
            null, null, null, ModerationActorType.User, "   ", null, Now, 30);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ModerationAudit.ActorRequired");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Create_WithInvalidRetention_ReturnsValidationError(int retentionDays)
    {
        Result<ModerationAuditRecord> result = CreateValid(retentionDays: retentionDays);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ModerationAudit.RetentionInvalid");
    }

    [Fact]
    public void Create_NormalizesCategoriesAndDropsBlanksAndDuplicates()
    {
        Result<ModerationAuditRecord> result = CreateValid(
            categories: [" hate ", "HATE", "", "   ", "violence"]);

        result.Value.FlaggedCategories.Should().Equal("hate", "violence");
    }

    [Fact]
    public void Create_CapsFlaggedCategoriesToTheMaximum()
    {
        string[] categories = Enumerable.Range(0, ModerationAuditRecord.MaxFlaggedCategories + 5)
            .Select(index => $"cat-{index}")
            .ToArray();

        Result<ModerationAuditRecord> result = CreateValid(categories: categories);

        result.Value.FlaggedCategories.Should().HaveCount(ModerationAuditRecord.MaxFlaggedCategories);
    }

    [Fact]
    public void Create_TruncatesOversizedFields_WithoutFailing()
    {
        Result<ModerationAuditRecord> result = ModerationAuditRecord.Create(
            Guid.NewGuid(),
            ModerationTargetType.Offer,
            ModerationVerdict.Rejected,
            new string('d', ModerationAuditRecord.MaxDecisionLength + 20),
            new string('c', ModerationAuditRecord.MaxContentContextLength + 20),
            null,
            null,
            new string('t', ModerationAuditRecord.MaxThresholdLength + 20),
            ModerationActorType.Administrator,
            new string('a', ModerationAuditRecord.MaxActorIdLength + 20),
            new string('r', ModerationAuditRecord.MaxCorrelationIdLength + 20),
            Now,
            30);

        ModerationAuditRecord record = result.Value;
        record.Decision.Should().HaveLength(ModerationAuditRecord.MaxDecisionLength);
        record.ContentContext.Should().HaveLength(ModerationAuditRecord.MaxContentContextLength);
        record.ThresholdApplied.Should().HaveLength(ModerationAuditRecord.MaxThresholdLength);
        record.ActorId.Should().HaveLength(ModerationAuditRecord.MaxActorIdLength);
        record.CorrelationId.Should().HaveLength(ModerationAuditRecord.MaxCorrelationIdLength);
    }

    [Fact]
    public void Create_WithoutScoresOrThreshold_KeepsEmptyEvidence()
    {
        Result<ModerationAuditRecord> result = ModerationAuditRecord.Create(
            Guid.NewGuid(), ModerationTargetType.MediaAsset, ModerationVerdict.Approved, "Approved", "media_upload",
            null, new Dictionary<string, double>(), "  ", ModerationActorType.System, "system", null, Now, 30);

        result.Value.CategoryScores.Should().BeEmpty();
        result.Value.ThresholdApplied.Should().BeNull();
        result.Value.FlaggedCategories.Should().BeEmpty();
        result.Value.CorrelationId.Should().BeEmpty();
    }

    [Fact]
    public void Rehydrate_PreservesIdentityAndStoredEvidence()
    {
        Guid id = Guid.NewGuid();
        Guid targetId = Guid.NewGuid();
        DateTime retainUntil = Now.AddDays(90);

        ModerationAuditRecord record = ModerationAuditRecord.Rehydrate(
            id,
            targetId,
            ModerationTargetType.FeedComment,
            ModerationVerdict.Rejected,
            "Rejected",
            "feed_comment",
            ["hate"],
            new Dictionary<string, double> { ["hate"] = 0.95 },
            "hate>=0.70",
            ModerationActorType.Administrator,
            "admin-1",
            "corr-9",
            Now,
            retainUntil);

        record.Id.Should().Be(id);
        record.TargetId.Should().Be(targetId);
        record.TargetType.Should().Be(ModerationTargetType.FeedComment);
        record.Verdict.Should().Be(ModerationVerdict.Rejected);
        record.CreatedAtUtc.Should().Be(Now);
        record.RetainUntilUtc.Should().Be(retainUntil);
        record.FlaggedCategories.Should().Equal("hate");
    }
}
