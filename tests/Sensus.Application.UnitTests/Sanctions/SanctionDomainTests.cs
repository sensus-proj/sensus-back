using FluentAssertions;
using Sensus.Domain.Entities;
using Sensus.Domain.Enums;
using Sensus.Domain.Policies;
using Sensus.Domain.ValueObjects;

namespace Sensus.Application.UnitTests.Sanctions;

/// <summary>Spec 020: vigencia, reversión, suspensión del usuario y escalamiento (FR-020-02, FR-020-03, FR-020-06).</summary>
public sealed class SanctionDomainTests
{
    private static readonly DateTime Now = new(2026, 11, 19, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid UserId = Guid.NewGuid();

    private static SanctionEvidence Evidence() =>
        new(Guid.NewGuid(), "Rejected", ["weapons"], new Dictionary<string, double> { ["weapons"] = 0.9 }, "0.60", "UserSuspended");

    private static Sanction Automatic(int? hours) =>
        Sanction.CreateAutomatic(UserId, "Contenido rechazado", Evidence(), Now, hours).Value;

    [Fact]
    public void CreateAutomatic_WithDuration_SetsEndAndSystemActor()
    {
        var sanction = Automatic(24);

        sanction.EndsAtUtc.Should().Be(Now.AddHours(24));
        sanction.AppliedBy.Should().Be(Sanction.SystemActor);
        sanction.Type.Should().Be(SanctionType.Automatic);
        sanction.IsPermanent.Should().BeFalse();
        sanction.Evidence.Categories.Should().Contain("weapons");
    }

    [Fact]
    public void CreateAutomatic_WithoutDuration_IsPermanent()
    {
        var sanction = Automatic(null);

        sanction.IsPermanent.Should().BeTrue();
        sanction.GetState(Now.AddYears(5)).Should().Be(SanctionState.Active);
    }

    [Fact]
    public void CreateManual_RecordsAdministratorAsResponsible()
    {
        var admin = Guid.NewGuid();

        var sanction = Sanction.CreateManual(UserId, admin, "Acoso reiterado", Now, 48).Value;

        sanction.AppliedBy.Should().Be(admin.ToString("D"));
        sanction.Type.Should().Be(SanctionType.Manual);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithoutReason_Fails(string reason)
    {
        var result = Sanction.CreateAutomatic(UserId, reason, Evidence(), Now, 24);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Sanction.ReasonRequired");
    }

    [Fact]
    public void Create_WithReasonOverLimit_Fails()
    {
        var result = Sanction.CreateAutomatic(UserId, new string('x', Sanction.MaxReasonLength + 1), Evidence(), Now, 24);

        result.Error.Code.Should().Be("Sanction.ReasonTooLong");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Create_WithNonPositiveDuration_Fails(int hours)
    {
        Sanction.CreateAutomatic(UserId, "x", Evidence(), Now, hours).Error.Code.Should().Be("Sanction.DurationInvalid");
    }

    [Fact]
    public void Create_WithEmptyUser_Fails()
    {
        Sanction.CreateAutomatic(Guid.Empty, "x", Evidence(), Now, 24).Error.Code.Should().Be("Sanction.UserRequired");
    }

    [Fact]
    public void GetState_AtExactEnd_IsExpired()
    {
        var sanction = Automatic(24);

        sanction.GetState(Now.AddHours(24).AddTicks(-1)).Should().Be(SanctionState.Active);
        sanction.GetState(Now.AddHours(24)).Should().Be(SanctionState.Expired);
    }

    [Fact]
    public void Revert_WhenActive_RecordsResponsibleAndKeepsHistory()
    {
        var sanction = Automatic(24);
        var admin = Guid.NewGuid();

        var result = sanction.Revert(admin, "Falso positivo", Now.AddHours(1));

        result.IsSuccess.Should().BeTrue();
        sanction.GetState(Now.AddHours(1)).Should().Be(SanctionState.Reverted);
        sanction.RevertedBy.Should().Be(admin);
        sanction.RevertReason.Should().Be("Falso positivo");
        sanction.Evidence.Decision.Should().Be("Rejected");
        sanction.Reason.Should().NotBeEmpty();
    }

    [Fact]
    public void Revert_WithoutReason_Fails()
    {
        Automatic(24).Revert(Guid.NewGuid(), " ", Now).Error.Code.Should().Be("Sanction.RevertReasonRequired");
    }

    [Fact]
    public void Revert_Twice_ReturnsConflict()
    {
        var sanction = Automatic(24);
        sanction.Revert(Guid.NewGuid(), "uno", Now);

        var second = sanction.Revert(Guid.NewGuid(), "dos", Now);

        second.Error.Code.Should().Be("Sanction.NotActive");
        second.Error.Type.Should().Be(Sensus.Domain.Common.ErrorType.Conflict);
    }

    [Fact]
    public void Revert_WhenExpired_ReturnsConflict()
    {
        Automatic(1).Revert(Guid.NewGuid(), "tarde", Now.AddHours(2)).Error.Code.Should().Be("Sanction.NotActive");
    }

    [Theory]
    [InlineData(0, 24)]
    [InlineData(1, 168)]
    [InlineData(2, 720)]
    public void Escalation_ReturnsTheStepMatchingPriorSanctions(int prior, int expectedHours)
    {
        var steps = new[] { new SanctionStep(24), new SanctionStep(168), new SanctionStep(720), new SanctionStep(null) };

        SanctionEscalationPolicy.Resolve(prior, steps).Hours.Should().Be(expectedHours);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(40)]
    public void Escalation_BeyondTheLadder_IsPermanent(int prior)
    {
        var steps = new[] { new SanctionStep(24), new SanctionStep(168), new SanctionStep(720), new SanctionStep(null) };

        SanctionEscalationPolicy.Resolve(prior, steps).IsPermanent.Should().BeTrue();
    }

    [Fact]
    public void Escalation_WithNegativeHistory_UsesFirstStep()
    {
        SanctionEscalationPolicy.Resolve(-3, [new SanctionStep(24)]).Hours.Should().Be(24);
    }

    [Fact]
    public void Escalation_WithEmptyLadder_Throws()
    {
        var act = () => SanctionEscalationPolicy.Resolve(0, []);

        act.Should().Throw<ArgumentException>();
    }

    // --- Suspensión del usuario (FR-020-04, NFR-020-01, CA-020-06) ---

    private static User NewUser() => User.Create("Ana", "ana@sensus.com", "hash", true, Now).Value;

    [Fact]
    public void User_WithoutSuspension_IsNotSuspended()
    {
        NewUser().IsSuspended(Now).Should().BeFalse();
    }

    [Fact]
    public void Suspend_BlocksUntilExactEnd_AndRotatesSecurityStamp()
    {
        var user = NewUser();
        var stamp = user.SecurityStamp;

        user.Suspend(Guid.NewGuid(), Now.AddHours(24), Now);

        user.SecurityStamp.Should().NotBe(stamp);
        user.IsSuspended(Now.AddHours(24).AddTicks(-1)).Should().BeTrue();
        user.IsSuspended(Now.AddHours(24)).Should().BeFalse();
    }

    [Fact]
    public void Suspend_Permanent_NeverExpires()
    {
        var user = NewUser();

        user.Suspend(Guid.NewGuid(), null, Now);

        user.IsSuspended(Now.AddYears(50)).Should().BeTrue();
    }

    [Fact]
    public void Suspend_WithShorterSanction_KeepsTheMoreSevereOne()
    {
        var user = NewUser();
        var long30 = Guid.NewGuid();
        user.Suspend(long30, Now.AddDays(30), Now);

        user.Suspend(Guid.NewGuid(), Now.AddHours(24), Now);

        user.Suspension!.SanctionId.Should().Be(long30);
    }

    [Fact]
    public void Suspend_WithPermanent_ReplacesTemporary()
    {
        var user = NewUser();
        user.Suspend(Guid.NewGuid(), Now.AddDays(30), Now);
        var permanent = Guid.NewGuid();

        user.Suspend(permanent, null, Now);

        user.Suspension!.SanctionId.Should().Be(permanent);
        user.Suspension.EndsAtUtc.Should().BeNull();
    }

    [Fact]
    public void LiftSuspension_OnlyAppliesToTheMatchingSanction_AndKeepsUserData()
    {
        var user = NewUser();
        var sanctionId = Guid.NewGuid();
        user.Suspend(sanctionId, Now.AddHours(24), Now);

        user.LiftSuspension(Guid.NewGuid()).Should().BeFalse();
        user.IsSuspended(Now).Should().BeTrue();

        user.LiftSuspension(sanctionId).Should().BeTrue();
        user.IsSuspended(Now).Should().BeFalse();
        user.Correo.Should().Be("ana@sensus.com");
        user.Estatus.Should().BeTrue();
    }

    [Fact]
    public void RestoreSuspension_DoesNotRotateSecurityStamp()
    {
        var user = NewUser();
        var stamp = user.SecurityStamp;

        user.RestoreSuspension(Guid.NewGuid(), Now.AddDays(1));

        user.SecurityStamp.Should().Be(stamp);
        user.IsSuspended(Now).Should().BeTrue();
    }

    [Fact]
    public void Rehydrate_RestoresEveryPersistedField()
    {
        var id = Guid.NewGuid();
        var admin = Guid.NewGuid();

        var sanction = Sanction.Rehydrate(
            id, UserId, SanctionType.Manual, "motivo", Evidence(), admin.ToString("D"),
            Now, Now.AddHours(5), admin, Now.AddHours(1), "revisión");

        sanction.Id.Should().Be(id);
        sanction.GetState(Now.AddHours(2)).Should().Be(SanctionState.Reverted);
        sanction.RevertReason.Should().Be("revisión");
        sanction.EndsAtUtc.Should().Be(Now.AddHours(5));
    }

    [Fact]
    public void Revert_WithReasonOverLimit_Fails()
    {
        Automatic(24).Revert(Guid.NewGuid(), new string('x', Sanction.MaxReasonLength + 1), Now)
            .Error.Code.Should().Be("Sanction.ReasonTooLong");
    }
}
