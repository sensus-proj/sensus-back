using FluentAssertions;
using NSubstitute;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Common.Models;
using Sensus.Application.Moderation.Audit;
using Sensus.Domain.Common;
using Sensus.Domain.Entities;
using Sensus.Domain.Enums;
using Sensus.Domain.Repositories;
using Xunit;

namespace Sensus.Application.UnitTests.Moderation.Audit;

/// <summary>
/// Pruebas unitarias para el handler de consulta de auditoría de moderación (Spec 021).
/// Ref: FR-021-03 - consulta paginada por administrador; CA-021-03 - 403 sin permisos.
/// </summary>
public sealed class GetModerationAuditQueryHandlerTests
{
    private static readonly DateTime Now = new(2026, 11, 22, 10, 0, 0, DateTimeKind.Utc);

    private readonly IModerationAuditRepository _repository = Substitute.For<IModerationAuditRepository>();
    private readonly ICurrentUserService _currentUser = Substitute.For<ICurrentUserService>();
    private readonly GetModerationAuditQueryHandler _handler;

    public GetModerationAuditQueryHandlerTests()
    {
        _currentUser.Roles.Returns([RoleType.Administrador]);
        _currentUser.UserId.Returns(Guid.NewGuid().ToString());
        _handler = new GetModerationAuditQueryHandler(_repository, _currentUser);
    }

    private static ModerationAuditRecord CreateSampleRecord(
        Guid? targetId = null,
        ModerationTargetType targetType = ModerationTargetType.Offer,
        ModerationVerdict verdict = ModerationVerdict.Approved) =>
        ModerationAuditRecord.Create(
            targetId ?? Guid.NewGuid(),
            targetType,
            verdict,
            verdict.ToString(),
            "test_context",
            ["cat1"],
            new Dictionary<string, double> { ["cat1"] = 0.5 },
            "threshold_1",
            ModerationActorType.User,
            "user-1",
            "corr-1",
            Now,
            30).Value;

    [Fact]
    public async Task Handle_WhenCallerIsAdmin_ReturnsPagedAuditRecords()
    {
        // Ref: FR-021-03 - consulta administrativa exitosa
        var record = CreateSampleRecord();
        _repository.ListAsync(
            Arg.Any<Guid?>(),
            Arg.Any<ModerationTargetType?>(),
            Arg.Any<ModerationVerdict?>(),
            Arg.Any<DateTime?>(),
            Arg.Any<DateTime?>(),
            1,
            20,
            Arg.Any<CancellationToken>())
            .Returns(([record], 1L));

        var query = new GetModerationAuditQuery(Page: 1, PageSize: 20);
        var result = await _handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().HaveCount(1);
        result.Value.TotalCount.Should().Be(1);
        result.Value.PageNumber.Should().Be(1);
        result.Value.PageSize.Should().Be(20);

        ModerationAuditRecordDto dto = result.Value.Items[0];
        dto.Id.Should().Be(record.Id);
        dto.TargetId.Should().Be(record.TargetId);
        dto.TargetType.Should().Be(record.TargetType.ToString());
        dto.Verdict.Should().Be(record.Verdict.ToString());
        dto.Decision.Should().Be(record.Decision);
        dto.ContentContext.Should().Be(record.ContentContext);
        dto.FlaggedCategories.Should().Equal(record.FlaggedCategories);
        dto.CategoryScores.Should().BeEquivalentTo(record.CategoryScores);
        dto.ThresholdApplied.Should().Be(record.ThresholdApplied);
        dto.ActorType.Should().Be(record.ActorType.ToString());
        dto.ActorId.Should().Be(record.ActorId);
        dto.CorrelationId.Should().Be(record.CorrelationId);
        dto.CreatedAtUtc.Should().Be(record.CreatedAtUtc);
        dto.RetainUntilUtc.Should().Be(record.RetainUntilUtc);
    }

    [Theory]
    [InlineData("Consumidor")]
    [InlineData("Comerciante")]
    [InlineData("Invitado")]
    public async Task Handle_WhenCallerIsNotAdmin_ReturnsForbiddenError(string role)
    {
        // Ref: CA-021-03 - 403 sin permisos
        _currentUser.Roles.Returns([role]);

        var query = new GetModerationAuditQuery();
        var result = await _handler.Handle(query, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Forbidden);
        result.Error.Code.Should().Be("ModerationAudit.AdminRequired");
    }

    [Fact]
    public async Task Handle_WhenCallerHasNoRoles_ReturnsForbiddenError()
    {
        // Ref: CA-021-03 - 403 sin permisos
        _currentUser.Roles.Returns([]);

        var query = new GetModerationAuditQuery();
        var result = await _handler.Handle(query, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Forbidden);
    }

    [Fact]
    public async Task Handle_WithFilters_PassesParsedEnumsAndDateRangeToRepository()
    {
        // Ref: FR-021-03 - filtros de objetivo, veredicto y rango temporal
        Guid targetId = Guid.NewGuid();
        DateTime from = Now.AddDays(-7);
        DateTime to = Now;

        _repository.ListAsync(
            targetId,
            ModerationTargetType.FeedComment,
            ModerationVerdict.Rejected,
            from,
            to,
            2,
            50,
            Arg.Any<CancellationToken>())
            .Returns(([], 0L));

        var query = new GetModerationAuditQuery(
            TargetId: targetId,
            TargetType: "FeedComment",
            Verdict: "Rejected",
            FromUtc: from,
            ToUtc: to,
            Page: 2,
            PageSize: 50);

        var result = await _handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().BeEmpty();
        result.Value.TotalCount.Should().Be(0);

        await _repository.Received(1).ListAsync(
            targetId,
            ModerationTargetType.FeedComment,
            ModerationVerdict.Rejected,
            from,
            to,
            2,
            50,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithUnknownEnumStrings_PassesNullEnumsToRepository()
    {
        // Ref: FR-021-03 - resiliencia ante valores no coincidentes
        _repository.ListAsync(
            Arg.Any<Guid?>(),
            null,
            null,
            Arg.Any<DateTime?>(),
            Arg.Any<DateTime?>(),
            1,
            20,
            Arg.Any<CancellationToken>())
            .Returns(([], 0L));

        var query = new GetModerationAuditQuery(
            TargetType: "UnknownTarget",
            Verdict: "UnknownVerdict");

        var result = await _handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await _repository.Received(1).ListAsync(
            null,
            null,
            null,
            null,
            null,
            1,
            20,
            Arg.Any<CancellationToken>());
    }
}
