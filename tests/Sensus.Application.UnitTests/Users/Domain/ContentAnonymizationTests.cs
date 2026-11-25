using FluentAssertions;
using Sensus.Domain.Entities;
using Sensus.Domain.Enums;
using Sensus.Domain.ValueObjects;

namespace Sensus.Application.UnitTests.Users.Domain;

/// <summary>Anonimización de contenido y auditoría sin PII (Spec 061, FR-061-01, FR-061-02 y FR-061-05).</summary>
public class ContentAnonymizationTests
{
    private static readonly DateTime Now = new(2026, 11, 23, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void FeedPost_Anonymize_ShouldKeepContentAndRemoveAuthorPii_Idempotent()
    {
        var author = Guid.NewGuid();
        FeedPost post = FeedPost.Create(
            author, "Ana Perez", "Contenido moderado", null, Now.AddDays(-2), authorPhotoUrl: "https://cdn/ana.jpg").Value;

        bool first = post.Anonymize(Now);

        first.Should().BeTrue();
        post.AuthorUserId.Should().Be(Guid.Empty);
        post.AuthorDisplayName.Should().Be(FeedPost.DefaultAuthorDisplayName);
        post.AuthorPhotoUrl.Should().BeNull();
        post.Content.Should().Be("Contenido moderado");
        post.ImageUrl.Should().BeNull();
        post.UpdatedAtUtc.Should().Be(Now);

        bool second = post.Anonymize(Now.AddDays(1));

        second.Should().BeFalse();
        post.UpdatedAtUtc.Should().Be(Now);
    }

    [Fact]
    public void FeedComment_Anonymize_ShouldKeepModeratedText_Idempotent()
    {
        var author = Guid.NewGuid();
        FeedComment comment = FeedComment.Create(
            Guid.NewGuid(), author, "Ana Perez", "Comentario aprobado", Now.AddDays(-1), authorPhotoUrl: "https://cdn/ana.jpg").Value;

        bool first = comment.Anonymize();
        bool second = comment.Anonymize();

        first.Should().BeTrue();
        second.Should().BeFalse();
        comment.AuthorUserId.Should().Be(Guid.Empty);
        comment.AuthorDisplayName.Should().Be(FeedComment.DefaultAuthorDisplayName);
        comment.AuthorPhotoUrl.Should().BeNull();
        comment.Text.Should().Be("Comentario aprobado");
    }

    [Fact]
    public void ModerationAudit_FromSanction_ShouldContainOnlyPseudonymAndTechnicalMetadata()
    {
        var userId = Guid.NewGuid();
        var evidence = new SanctionEvidence(
            Guid.NewGuid(),
            "Rejected",
            ["violence"],
            new Dictionary<string, double> { ["violence"] = 0.93 },
            "0.85",
            "Quarantine");
        Sanction sanction = Sanction.CreateAutomatic(userId, "Contenido rechazado", evidence, Now, null).Value;
        string subjectKey = ModerationAudit.ComputeSubjectKey(userId);

        ModerationAudit? entry = ModerationAudit.FromSanction(sanction, subjectKey, TimeSpan.FromDays(90), Now);

        entry.Should().NotBeNull();
        entry!.SubjectKey.Should().Be(subjectKey);
        entry.SubjectKey.Should().NotContain(userId.ToString());
        entry.SubjectKey.Should().HaveLength(64);
        entry.SourceSanctionId.Should().Be(sanction.Id);
        entry.Decision.Should().Be("Rejected");
        entry.Categories.Should().ContainSingle().Which.Should().Be("violence");
        entry.Scores["violence"].Should().Be(0.93);
        entry.ThresholdApplied.Should().Be("0.85");
        entry.Action.Should().Be("Quarantine");
        entry.ExpiresAtUtc.Should().Be(Now.AddDays(90));
        entry.ToString().Should().NotContain("ana@sensus.com");

        ModerationAudit? second = ModerationAudit.FromSanction(sanction, subjectKey, TimeSpan.FromDays(90), Now);
        second!.Id.Should().Be(entry.Id);
    }

    [Fact]
    public void ModerationAudit_FromSanction_ShouldSkipManualSanctions()
    {
        var userId = Guid.NewGuid();
        Sanction manual = Sanction.CreateManual(userId, Guid.NewGuid(), "Revisión", Now, null).Value;

        ModerationAudit? entry = ModerationAudit.FromSanction(
            manual,
            ModerationAudit.ComputeSubjectKey(userId),
            TimeSpan.FromDays(90),
            Now);

        entry.Should().BeNull();
    }
}
