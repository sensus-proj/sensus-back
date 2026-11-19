using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Common.Settings;
using Sensus.Application.Sanctions.Commands.ApplySanction;
using Sensus.Application.Sanctions.Commands.CreateManualSanction;
using Sensus.Application.Sanctions.Commands.RevertSanction;
using Sensus.Application.Sanctions.Common;
using Sensus.Application.Sanctions.Queries;
using Sensus.Application.Sanctions.Services;
using Sensus.Domain.Common;
using Sensus.Domain.Entities;
using Sensus.Domain.Enums;
using Sensus.Domain.Repositories;
using Sensus.Domain.ValueObjects;

namespace Sensus.Application.UnitTests.Sanctions;

/// <summary>Spec 020: casos de uso de aplicar, sancionar manualmente, revertir y consultar.</summary>
public sealed class SanctionHandlersTests
{
    private static readonly DateTime Now = new(2026, 11, 19, 12, 0, 0, DateTimeKind.Utc);

    private readonly ISanctionRepository _sanctions = Substitute.For<ISanctionRepository>();
    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly ICurrentUserService _currentUser = Substitute.For<ICurrentUserService>();
    private readonly ISanctionNotifier _notifier = Substitute.For<ISanctionNotifier>();
    private readonly ISanctionMetrics _metrics = Substitute.For<ISanctionMetrics>();
    private readonly IDateTimeProvider _clock = Substitute.For<IDateTimeProvider>();
    private readonly SanctionSettings _settings = new();

    private readonly Guid _adminId = Guid.NewGuid();
    private readonly User _user = User.Create("Ana", "ana@sensus.com", "hash", true, Now).Value;

    public SanctionHandlersTests()
    {
        _clock.UtcNow.Returns(Now);
        _users.GetByIdAsync(_user.Id, Arg.Any<CancellationToken>()).Returns(_user);
        _currentUser.UserId.Returns(_adminId.ToString());
        _currentUser.Roles.Returns([RoleType.Administrador]);
    }

    private ApplySanctionCommandHandler ApplyHandler() => new(
        _sanctions, _users, _notifier, _metrics, _clock, Options.Create(_settings), NullLogger<ApplySanctionCommandHandler>.Instance);

    private ApplySanctionCommand Rejected(Guid? mediaId = null) =>
        new(_user.Id, mediaId ?? Guid.NewGuid(), "Rejected", ["weapons"], new Dictionary<string, double> { ["weapons"] = 0.9 }, "weapons>=0.60");

    // --- ApplySanction (FR-020-01, FR-020-03, FR-020-06) ---

    [Fact]
    public async Task Apply_FirstRejection_Suspends24HoursRecordsEvidenceAndNotifies()
    {
        var mediaId = Guid.NewGuid();
        Sanction? saved = null;
        await _sanctions.AddAsync(Arg.Do<Sanction>(s => saved = s), Arg.Any<CancellationToken>());

        var result = await ApplyHandler().Handle(Rejected(mediaId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        saved.Should().NotBeNull();
        saved!.EndsAtUtc.Should().Be(Now.AddHours(24));
        saved.AppliedBy.Should().Be(Sanction.SystemActor);
        saved.Evidence.MediaId.Should().Be(mediaId);
        saved.Evidence.Categories.Should().Contain("weapons");
        saved.Evidence.ThresholdApplied.Should().Be("weapons>=0.60");
        _user.IsSuspended(Now).Should().BeTrue();
        await _users.Received(1).UpdateAsync(_user, Arg.Any<CancellationToken>());
        await _notifier.Received(1).NotifyAppliedAsync(saved, Arg.Any<CancellationToken>());
        _metrics.Received(1).SanctionApplied(true);
    }

    [Theory]
    [InlineData(1, 168)]
    [InlineData(2, 720)]
    public async Task Apply_Recidivism_EscalatesDuration(int prior, int expectedHours)
    {
        _sanctions.CountNotRevertedSinceAsync(_user.Id, Arg.Any<DateTime>(), Arg.Any<CancellationToken>()).Returns(prior);
        Sanction? saved = null;
        await _sanctions.AddAsync(Arg.Do<Sanction>(s => saved = s), Arg.Any<CancellationToken>());

        await ApplyHandler().Handle(Rejected(), CancellationToken.None);

        saved!.EndsAtUtc.Should().Be(Now.AddHours(expectedHours));
    }

    [Fact]
    public async Task Apply_FourthOffense_IsPermanent()
    {
        _sanctions.CountNotRevertedSinceAsync(_user.Id, Arg.Any<DateTime>(), Arg.Any<CancellationToken>()).Returns(3);
        Sanction? saved = null;
        await _sanctions.AddAsync(Arg.Do<Sanction>(s => saved = s), Arg.Any<CancellationToken>());

        await ApplyHandler().Handle(Rejected(), CancellationToken.None);

        saved!.IsPermanent.Should().BeTrue();
        _user.IsSuspended(Now.AddYears(10)).Should().BeTrue();
    }

    [Fact]
    public async Task Apply_UsesConfiguredWindow()
    {
        _settings.EscalationWindowDays = 30;

        await ApplyHandler().Handle(Rejected(), CancellationToken.None);

        await _sanctions.Received(1).CountNotRevertedSinceAsync(_user.Id, Now.AddDays(-30), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("Quarantine")]
    [InlineData("Approved")]
    public async Task Apply_NonTriggerDecision_DoesNothing(string decision)
    {
        var command = Rejected() with { Decision = decision };

        var result = await ApplyHandler().Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await _sanctions.DidNotReceive().AddAsync(Arg.Any<Sanction>(), Arg.Any<CancellationToken>());
        _user.IsSuspended(Now).Should().BeFalse();
    }

    [Fact]
    public async Task Apply_WhenDisabled_DoesNothing()
    {
        _settings.Enabled = false;

        await ApplyHandler().Handle(Rejected(), CancellationToken.None);

        await _sanctions.DidNotReceive().AddAsync(Arg.Any<Sanction>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Apply_ToAdministrator_IsSkipped()
    {
        _user.AddRole(RoleType.Administrador);

        var result = await ApplyHandler().Handle(Rejected(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await _sanctions.DidNotReceive().AddAsync(Arg.Any<Sanction>(), Arg.Any<CancellationToken>());
        _user.IsSuspended(Now).Should().BeFalse();
    }

    [Fact]
    public async Task Apply_UnknownUser_ReturnsNotFound()
    {
        var command = Rejected() with { UserId = Guid.NewGuid() };

        var result = await ApplyHandler().Handle(command, CancellationToken.None);

        result.Error.Code.Should().Be("Sanction.UserNotFound");
    }

    [Fact]
    public async Task Apply_RepeatedForSameMedia_IsIdempotent()
    {
        var mediaId = Guid.NewGuid();
        var existing = Sanction.CreateAutomatic(
            _user.Id, "x", new SanctionEvidence(mediaId, "Rejected", [], new Dictionary<string, double>(), null, "a"), Now, 24).Value;
        _sanctions.GetByMediaIdAsync(mediaId, Arg.Any<CancellationToken>()).Returns(existing);

        var result = await ApplyHandler().Handle(Rejected(mediaId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await _sanctions.DidNotReceive().AddAsync(Arg.Any<Sanction>(), Arg.Any<CancellationToken>());
        _user.Suspension!.SanctionId.Should().Be(existing.Id, "el reintento repara una suspensión que no llegó a guardarse");
    }

    // --- CreateManualSanction (FR-020-07, CA-020-10) ---

    private CreateManualSanctionCommandHandler ManualHandler() => new(
        _sanctions, _users, _currentUser, _notifier, _metrics, _clock, NullLogger<CreateManualSanctionCommandHandler>.Instance);

    [Fact]
    public async Task Manual_ByAdministrator_BansPermanentlyWhenNoDuration()
    {
        var result = await ManualHandler().Handle(new CreateManualSanctionCommand(_user.Id, "Fraude", null), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.EndsAtUtc.Should().BeNull();
        result.Value.AppliedBy.Should().Be(_adminId.ToString("D"));
        result.Value.Status.Should().Be("Active");
        _user.IsSuspended(Now.AddYears(5)).Should().BeTrue();
        _metrics.Received(1).SanctionApplied(false);
    }

    [Fact]
    public async Task Manual_ByNonAdministrator_IsForbidden()
    {
        _currentUser.Roles.Returns([RoleType.Consumidor]);

        var result = await ManualHandler().Handle(new CreateManualSanctionCommand(_user.Id, "x", 24), CancellationToken.None);

        result.Error.Code.Should().Be("Sanction.AdminRequired");
        await _sanctions.DidNotReceive().AddAsync(Arg.Any<Sanction>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Manual_AgainstSelf_IsForbidden()
    {
        var result = await ManualHandler().Handle(new CreateManualSanctionCommand(_adminId, "x", 24), CancellationToken.None);

        result.Error.Code.Should().Be("Sanction.TargetNotAllowed");
    }

    [Fact]
    public async Task Manual_AgainstAnotherAdministrator_IsForbidden()
    {
        _user.AddRole(RoleType.Administrador);

        var result = await ManualHandler().Handle(new CreateManualSanctionCommand(_user.Id, "x", 24), CancellationToken.None);

        result.Error.Code.Should().Be("Sanction.TargetNotAllowed");
        _user.IsSuspended(Now).Should().BeFalse();
    }

    [Fact]
    public async Task Manual_UnknownUser_ReturnsNotFound()
    {
        var result = await ManualHandler().Handle(new CreateManualSanctionCommand(Guid.NewGuid(), "x", 24), CancellationToken.None);

        result.Error.Code.Should().Be("Sanction.UserNotFound");
    }

    [Fact]
    public void ManualValidator_RejectsEmptyReasonAndOutOfRangeDuration()
    {
        var validator = new CreateManualSanctionCommandValidator();

        validator.Validate(new CreateManualSanctionCommand(_user.Id, "", 24)).IsValid.Should().BeFalse();
        validator.Validate(new CreateManualSanctionCommand(_user.Id, "x", 0)).IsValid.Should().BeFalse();
        validator.Validate(new CreateManualSanctionCommand(_user.Id, "x", CreateManualSanctionCommandValidator.MaxDurationHours + 1)).IsValid.Should().BeFalse();
        validator.Validate(new CreateManualSanctionCommand(_user.Id, "x", null)).IsValid.Should().BeTrue();
    }

    // --- RevertSanction (FR-020-02, NFR-020-02) ---

    private RevertSanctionCommandHandler RevertHandler() => new(
        _sanctions, _users, _currentUser, _notifier, _metrics, _clock, NullLogger<RevertSanctionCommandHandler>.Instance);

    private Sanction SuspendUser(int hours)
    {
        var sanction = Sanction.CreateAutomatic(
            _user.Id, "x", SanctionEvidence.Manual, Now, hours).Value;
        _user.Suspend(sanction.Id, sanction.EndsAtUtc, Now);
        _sanctions.GetByIdAsync(sanction.Id, Arg.Any<CancellationToken>()).Returns(sanction);
        return sanction;
    }

    [Fact]
    public async Task Revert_RestoresAccessKeepsRecordAndNotifies()
    {
        var sanction = SuspendUser(24);

        var result = await RevertHandler().Handle(new RevertSanctionCommand(sanction.Id, "Falso positivo"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be("Reverted");
        result.Value.RevertedBy.Should().Be(_adminId);
        _user.IsSuspended(Now).Should().BeFalse();
        await _sanctions.Received(1).UpdateAsync(sanction, Arg.Any<CancellationToken>());
        await _notifier.Received(1).NotifyRevertedAsync(sanction, Arg.Any<CancellationToken>());
        _metrics.Received(1).SanctionReverted();
    }

    [Fact]
    public async Task Revert_WhenAnotherActiveSanctionRemains_KeepsUserBlocked()
    {
        var first = SuspendUser(24);
        var other = Sanction.CreateManual(_user.Id, _adminId, "otra", Now, 720).Value;
        _sanctions.GetActiveByUserAsync(_user.Id, Now, Arg.Any<CancellationToken>()).Returns([other]);

        await RevertHandler().Handle(new RevertSanctionCommand(first.Id, "revisión"), CancellationToken.None);

        _user.IsSuspended(Now).Should().BeTrue();
        _user.Suspension!.SanctionId.Should().Be(other.Id);
    }

    [Fact]
    public async Task Revert_AlreadyReverted_ReturnsConflict()
    {
        var sanction = SuspendUser(24);
        sanction.Revert(_adminId, "antes", Now);

        var result = await RevertHandler().Handle(new RevertSanctionCommand(sanction.Id, "otra vez"), CancellationToken.None);

        result.Error.Code.Should().Be("Sanction.NotActive");
        await _notifier.DidNotReceive().NotifyRevertedAsync(Arg.Any<Sanction>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Revert_UnknownSanction_ReturnsNotFound()
    {
        var result = await RevertHandler().Handle(new RevertSanctionCommand(Guid.NewGuid(), "x"), CancellationToken.None);

        result.Error.Code.Should().Be("Sanction.NotFound");
    }

    [Fact]
    public async Task Revert_ByNonAdministrator_IsForbidden()
    {
        var sanction = SuspendUser(24);
        _currentUser.Roles.Returns([RoleType.Consumidor]);

        var result = await RevertHandler().Handle(new RevertSanctionCommand(sanction.Id, "x"), CancellationToken.None);

        result.Error.Code.Should().Be("Sanction.AdminRequired");
        _user.IsSuspended(Now).Should().BeTrue();
    }

    // --- Consultas (FR-020-05, NFR-020-04) ---

    [Fact]
    public async Task List_ReturnsPagedDtos()
    {
        var sanction = Sanction.CreateManual(_user.Id, _adminId, "x", Now, 24).Value;
        _sanctions.ListAsync(_user.Id, SanctionState.Active, Now, 2, 10, Arg.Any<CancellationToken>())
            .Returns(((IReadOnlyList<Sanction>)[sanction], 11L));
        var handler = new ListSanctionsQueryHandler(_sanctions, _currentUser, _clock);

        var result = await handler.Handle(new ListSanctionsQuery(_user.Id, "active", 2, 10), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().ContainSingle();
        result.Value.TotalCount.Should().Be(11);
        result.Value.TotalPages.Should().Be(2);
    }

    [Fact]
    public async Task List_ByNonAdministrator_IsForbidden()
    {
        _currentUser.Roles.Returns([RoleType.Comerciante]);
        var handler = new ListSanctionsQueryHandler(_sanctions, _currentUser, _clock);

        var result = await handler.Handle(new ListSanctionsQuery(null, null), CancellationToken.None);

        result.Error.Code.Should().Be("Sanction.AdminRequired");
    }

    [Theory]
    [InlineData(0, 20, null, false)]
    [InlineData(1, 0, null, false)]
    [InlineData(1, 101, null, false)]
    [InlineData(1, 100, null, true)]
    [InlineData(1, 20, "Bogus", false)]
    [InlineData(1, 20, "reverted", true)]
    public void ListValidator_EnforcesPaginationAndStatus(int page, int pageSize, string? status, bool valid)
    {
        new ListSanctionsQueryValidator().Validate(new ListSanctionsQuery(null, status, page, pageSize)).IsValid.Should().Be(valid);
    }

    [Fact]
    public async Task Get_UnknownSanction_ReturnsNotFound()
    {
        var handler = new GetSanctionQueryHandler(_sanctions, _currentUser, _clock);

        var result = await handler.Handle(new GetSanctionQuery(Guid.NewGuid()), CancellationToken.None);

        result.Error.Code.Should().Be("Sanction.NotFound");
    }

    [Fact]
    public async Task Get_ReturnsDetail_WithoutAnyPersonalData()
    {
        var sanction = Sanction.CreateManual(_user.Id, _adminId, "x", Now, 24).Value;
        _sanctions.GetByIdAsync(sanction.Id, Arg.Any<CancellationToken>()).Returns(sanction);
        var handler = new GetSanctionQueryHandler(_sanctions, _currentUser, _clock);

        var result = await handler.Handle(new GetSanctionQuery(sanction.Id), CancellationToken.None);

        result.Value.Id.Should().Be(sanction.Id);
        result.Value.Evidence.Action.Should().Be("ManualSanction");
    }

    // --- Notificador (FR-020-08) ---

    [Fact]
    public async Task Notifier_SendsToEveryActiveDevice_WithoutEvidence()
    {
        var devices = Substitute.For<IDeviceRepository>();
        var push = Substitute.For<IPushDispatcher>();
        var sanction = Sanction.CreateAutomatic(_user.Id, "x", SanctionEvidence.Manual, Now, 24).Value;
        devices.GetActiveByUserAsync(_user.Id, Arg.Any<CancellationToken>()).Returns(
        [
            Device.Create(_user.Id, "d1", "android", "tok-1", Now),
            Device.Create(_user.Id, "d2", "android", "tok-2", Now)
        ]);
        var notifier = new SanctionNotifier(devices, push, NullLogger<SanctionNotifier>.Instance);

        await notifier.NotifyAppliedAsync(sanction, CancellationToken.None);

        await push.Received(2).SendAsync(
            Arg.Any<string>(),
            "Tu cuenta fue suspendida",
            Arg.Is<string>(b => b.Contains("20/11/2026 12:00 UTC") && !b.Contains("weapons")),
            Arg.Is<IReadOnlyDictionary<string, string>>(d => d["type"] == "sanction_applied"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Notifier_WhenPushFails_DoesNotThrow()
    {
        var devices = Substitute.For<IDeviceRepository>();
        var push = Substitute.For<IPushDispatcher>();
        devices.GetActiveByUserAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns<List<Device>>(_ => throw new InvalidOperationException("FCM caído"));
        var sanction = Sanction.CreateAutomatic(_user.Id, "x", SanctionEvidence.Manual, Now, null).Value;
        var notifier = new SanctionNotifier(devices, push, NullLogger<SanctionNotifier>.Instance);

        var act = () => notifier.NotifyRevertedAsync(sanction, CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Notifier_PermanentBan_SaysPermanent()
    {
        var devices = Substitute.For<IDeviceRepository>();
        var push = Substitute.For<IPushDispatcher>();
        devices.GetActiveByUserAsync(_user.Id, Arg.Any<CancellationToken>())
            .Returns([Device.Create(_user.Id, "d1", "android", "tok-1", Now)]);
        var sanction = Sanction.CreateAutomatic(_user.Id, "x", SanctionEvidence.Manual, Now, null).Value;

        await new SanctionNotifier(devices, push, NullLogger<SanctionNotifier>.Instance)
            .NotifyAppliedAsync(sanction, CancellationToken.None);

        await push.Received(1).SendAsync(
            "tok-1",
            Arg.Any<string>(),
            Arg.Is<string>(b => b.Contains("de forma permanente")),
            Arg.Any<IReadOnlyDictionary<string, string>>(),
            Arg.Any<CancellationToken>());
    }

    // --- Mapeo del error de login (CA-020-03) ---

    [Fact]
    public void LoginError_ExposesEndDate_ButNeverEvidence()
    {
        _user.Suspend(Guid.NewGuid(), Now.AddHours(24), Now);

        var error = _user.ToLoginError();

        error.Code.Should().Be("Auth.UserSanctioned");
        error.Type.Should().Be(ErrorType.Forbidden);
        error.Extensions![SanctionMapping.SanctionEndsAtKey].Should().Be(Now.AddHours(24));
        error.Message.Should().NotContain("weapons");
    }

    [Fact]
    public void LoginError_ForPermanentBan_HasNullEndDate()
    {
        _user.Suspend(Guid.NewGuid(), null, Now);

        _user.ToLoginError().Extensions![SanctionMapping.SanctionEndsAtKey].Should().BeNull();
    }

    [Fact]
    public void RevertValidator_RequiresIdAndReasonWithinLimit()
    {
        var validator = new RevertSanctionCommandValidator();

        validator.Validate(new RevertSanctionCommand(Guid.NewGuid(), "Falso positivo")).IsValid.Should().BeTrue();
        validator.Validate(new RevertSanctionCommand(Guid.Empty, "x")).IsValid.Should().BeFalse();
        validator.Validate(new RevertSanctionCommand(Guid.NewGuid(), "")).IsValid.Should().BeFalse();
        validator.Validate(new RevertSanctionCommand(Guid.NewGuid(), new string('x', Sanction.MaxReasonLength + 1))).IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Get_ByNonAdministrator_IsForbidden()
    {
        _currentUser.Roles.Returns([RoleType.Consumidor]);
        var handler = new GetSanctionQueryHandler(_sanctions, _currentUser, _clock);

        var result = await handler.Handle(new GetSanctionQuery(Guid.NewGuid()), CancellationToken.None);

        result.Error.Code.Should().Be("Sanction.AdminRequired");
    }

    [Fact]
    public async Task List_WithoutStatus_DoesNotFilterByState()
    {
        _sanctions.ListAsync(null, null, Now, 1, 20, Arg.Any<CancellationToken>())
            .Returns(((IReadOnlyList<Sanction>)[], 0L));
        var handler = new ListSanctionsQueryHandler(_sanctions, _currentUser, _clock);

        var result = await handler.Handle(new ListSanctionsQuery(null, null), CancellationToken.None);

        result.Value.Items.Should().BeEmpty();
        await _sanctions.Received(1).ListAsync(null, null, Now, 1, 20, Arg.Any<CancellationToken>());
    }
}
