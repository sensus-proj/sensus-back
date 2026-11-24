using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Common.Settings;
using Sensus.Application.Moderation.Audit;
using Sensus.Domain.Common;
using Sensus.Domain.Entities;
using Sensus.Domain.Enums;
using Sensus.Domain.Repositories;
using Xunit;

namespace Sensus.Application.UnitTests.Moderation.Audit;

/// <summary>
/// Pruebas unitarias para el grabador de auditoría de moderación (Spec 021).
/// Ref: FR-021-01 - registro de decisión, puntajes y umbral; FR-021-04 - retención configurada.
/// </summary>
public sealed class ModerationAuditRecorderTests
{
    private static readonly DateTime Now = new(2026, 11, 22, 10, 0, 0, DateTimeKind.Utc);

    private readonly IModerationAuditRepository _repository = Substitute.For<IModerationAuditRepository>();
    private readonly ICurrentUserService _currentUser = Substitute.For<ICurrentUserService>();
    private readonly ICorrelationIdAccessor _correlation = Substitute.For<ICorrelationIdAccessor>();
    private readonly IDateTimeProvider _clock = Substitute.For<IDateTimeProvider>();
    private readonly IOptions<ModerationAuditSettings> _settings = Substitute.For<IOptions<ModerationAuditSettings>>();
    private readonly ILogger<ModerationAuditRecorder> _logger = Substitute.For<ILogger<ModerationAuditRecorder>>();
    private readonly ModerationAuditRecorder _recorder;

    public ModerationAuditRecorderTests()
    {
        _clock.UtcNow.Returns(Now);
        _correlation.CorrelationId.Returns("corr-123");
        _settings.Value.Returns(new ModerationAuditSettings { RetentionDays = 365 });
        _currentUser.IsAuthenticated.Returns(true);
        _currentUser.UserId.Returns(Guid.NewGuid().ToString());
        _currentUser.Roles.Returns([RoleType.Consumidor]);

        _recorder = new ModerationAuditRecorder(
            _repository,
            _currentUser,
            _correlation,
            _clock,
            _settings,
            _logger);
    }

    [Fact]
    public async Task RecordAsync_WithValidEntry_PersistsRecordToRepository()
    {
        // Ref: FR-021-01 - persistencia exitosa
        Guid targetId = Guid.NewGuid();
        var entry = new ModerationAuditEntry(
            targetId,
            ModerationTargetType.MediaAsset,
            ModerationVerdict.Approved,
            "Approved",
            "media_upload",
            ["cat1"],
            new Dictionary<string, double> { ["cat1"] = 0.1 },
            "threshold_ok");

        Result result = await _recorder.RecordAsync(entry, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await _repository.Received(1).AddAsync(
            Arg.Is<ModerationAuditRecord>(r =>
                r.TargetId == targetId
                && r.TargetType == ModerationTargetType.MediaAsset
                && r.Verdict == ModerationVerdict.Approved
                && r.Decision == "Approved"
                && r.ContentContext == "media_upload"
                && r.ThresholdApplied == "threshold_ok"
                && r.CorrelationId == "corr-123"
                && r.CreatedAtUtc == Now
                && r.RetainUntilUtc == Now.AddDays(365)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RecordAsync_WhenDomainValidationFails_ReturnsFailure()
    {
        // Ref: FR-021-01 - rechazo de evidencia inválida
        var invalidEntry = new ModerationAuditEntry(
            Guid.Empty,
            ModerationTargetType.Offer,
            ModerationVerdict.Rejected,
            "Rejected",
            "context");

        Result result = await _recorder.RecordAsync(invalidEntry, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ModerationAudit.TargetRequired");
        await _repository.DidNotReceive().AddAsync(Arg.Any<ModerationAuditRecord>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RecordAsync_WhenRepositoryThrowsException_ReturnsUnavailableError()
    {
        // Ref: FR-021-01 - resiliencia y fail-closed ante fallo de persistencia
        _repository.AddAsync(Arg.Any<ModerationAuditRecord>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("MongoDB timeout"));

        var entry = new ModerationAuditEntry(
            Guid.NewGuid(),
            ModerationTargetType.FeedComment,
            ModerationVerdict.Approved,
            "Approved",
            "feed_comment");

        Result result = await _recorder.RecordAsync(entry, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ModerationAudit.Unavailable");
        result.Error.Type.Should().Be(ErrorType.Unavailable);
    }

    [Fact]
    public async Task RecordAsync_WhenOperationCanceled_PropagatesException()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        _repository.AddAsync(Arg.Any<ModerationAuditRecord>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new OperationCanceledException(cts.Token));

        var entry = new ModerationAuditEntry(
            Guid.NewGuid(),
            ModerationTargetType.Offer,
            ModerationVerdict.Approved,
            "Approved",
            "offer_create");

        Func<Task> action = async () => await _recorder.RecordAsync(entry, cts.Token);

        await action.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task RecordBestEffortAsync_WhenRepositoryFails_DoesNotThrow()
    {
        // Ref: FR-021-01 - mejor esfuerzo para contenido rechazado
        _repository.AddAsync(Arg.Any<ModerationAuditRecord>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("DB down"));

        var entry = new ModerationAuditEntry(
            Guid.NewGuid(),
            ModerationTargetType.Offer,
            ModerationVerdict.Rejected,
            "Rejected",
            "offer_create");

        Func<Task> action = async () => await _recorder.RecordBestEffortAsync(entry, CancellationToken.None);

        await action.Should().NotThrowAsync();
    }

    [Fact]
    public async Task RecordAsync_WhenActorExplicitlyProvided_UsesExplicitActor()
    {
        var entry = new ModerationAuditEntry(
            Guid.NewGuid(),
            ModerationTargetType.MediaAsset,
            ModerationVerdict.Approved,
            "Approved",
            "media_upload",
            ActorType: ModerationActorType.Administrator,
            ActorId: "admin-custom-id");

        await _recorder.RecordAsync(entry, CancellationToken.None);

        await _repository.Received(1).AddAsync(
            Arg.Is<ModerationAuditRecord>(r =>
                r.ActorType == ModerationActorType.Administrator
                && r.ActorId == "admin-custom-id"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RecordAsync_WhenUserIsAdmin_ResolvesToAdministratorActor()
    {
        string userId = Guid.NewGuid().ToString();
        _currentUser.UserId.Returns(userId);
        _currentUser.Roles.Returns([RoleType.Administrador]);

        var entry = new ModerationAuditEntry(
            Guid.NewGuid(),
            ModerationTargetType.MediaAsset,
            ModerationVerdict.Approved,
            "Approved",
            "media_upload");

        await _recorder.RecordAsync(entry, CancellationToken.None);

        await _repository.Received(1).AddAsync(
            Arg.Is<ModerationAuditRecord>(r =>
                r.ActorType == ModerationActorType.Administrator
                && r.ActorId == userId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RecordAsync_WhenUnauthenticated_ResolvesToSystemActor()
    {
        _currentUser.IsAuthenticated.Returns(false);
        _currentUser.UserId.Returns((string?)null);

        var entry = new ModerationAuditEntry(
            Guid.NewGuid(),
            ModerationTargetType.MediaAsset,
            ModerationVerdict.Approved,
            "Approved",
            "media_upload");

        await _recorder.RecordAsync(entry, CancellationToken.None);

        await _repository.Received(1).AddAsync(
            Arg.Is<ModerationAuditRecord>(r =>
                r.ActorType == ModerationActorType.System
                && r.ActorId == ModerationAuditRecorder.SystemActorId),
            Arg.Any<CancellationToken>());
    }
}
