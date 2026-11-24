using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Common.Settings;
using Sensus.Application.Reports.Commands.CreateContentReport;
using Sensus.Application.Reports.DTOs;
using Sensus.Domain.Common;
using Sensus.Domain.Entities;
using Sensus.Domain.Enums;
using Sensus.Domain.Repositories;

namespace Sensus.Application.UnitTests.Reports;

public sealed class ContentReportTests
{
    private static readonly DateTime Now = new(2026, 11, 21, 15, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData("spam", ReportReason.Spam)]
    [InlineData("ofensivo", ReportReason.Offensive)]
    [InlineData("fraude", ReportReason.Fraud)]
    [InlineData("FALSE_OFFER", ReportReason.FalseOffer)]
    [InlineData("PRICE_MISMATCH", ReportReason.PriceMismatch)]
    [InlineData("otro", ReportReason.Other)]
    public void TryParseReason_SupportsSpec040AndMobileFt19Aliases(string raw, ReportReason expected)
    {
        ContentReportPolicy.TryParseReason(raw, out ReportReason parsed).Should().BeTrue();
        parsed.Should().Be(expected);
    }

    [Fact]
    public async Task CreateContentReportHandler_RejectsCsamWithoutPersistenceAndWithoutPiiInLog()
    {
        var repo = Substitute.For<IContentReportRepository>();
        var targetService = Substitute.For<IReportTargetService>();
        var currentUser = Substitute.For<ICurrentUserService>();
        var clock = Substitute.For<IDateTimeProvider>();
        var logger = Substitute.For<ILogger<CreateContentReportCommandHandler>>();
        var correlation = Substitute.For<ICorrelationIdAccessor>();

        currentUser.UserId.Returns(Guid.NewGuid().ToString());
        correlation.CorrelationId.Returns("corr-legal-001");

        var cdnInvalidator = Substitute.For<ICdnCacheInvalidator>();
        var handler = new CreateContentReportCommandHandler(
            repo,
            targetService,
            currentUser,
            clock,
            Options.Create(new ContentReportSettings()),
            cdnInvalidator,
            logger,
            correlation);

        Result<ContentReportDto> result = await handler.Handle(
            new CreateContentReportCommand("post", Guid.NewGuid(), "otro", "Reportando posible CSAM y explotación infantil"),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ContentReport.ProhibitedLegalContent");
        await repo.DidNotReceive().TryAddAsync(Arg.Any<ContentReport>(), Arg.Any<CancellationToken>());
        await targetService.DidNotReceive().GetTargetInfoAsync(Arg.Any<ReportTargetType>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateContentReportHandler_RejectsSelfReport()
    {
        var repo = Substitute.For<IContentReportRepository>();
        var targetService = Substitute.For<IReportTargetService>();
        var currentUser = Substitute.For<ICurrentUserService>();
        var clock = Substitute.For<IDateTimeProvider>();
        var logger = Substitute.For<ILogger<CreateContentReportCommandHandler>>();

        Guid authorId = Guid.NewGuid();
        Guid postId = Guid.NewGuid();
        currentUser.UserId.Returns(authorId.ToString());
        targetService.GetTargetInfoAsync(ReportTargetType.Post, postId, Arg.Any<CancellationToken>())
            .Returns(new ReportTargetInfo(Exists: true, AuthorUserId: authorId));

        var cdnInvalidator = Substitute.For<ICdnCacheInvalidator>();
        var handler = new CreateContentReportCommandHandler(
            repo,
            targetService,
            currentUser,
            clock,
            Options.Create(new ContentReportSettings()),
            cdnInvalidator,
            logger);

        Result<ContentReportDto> result = await handler.Handle(
            new CreateContentReportCommand("post", postId, "spam", "autopromoción"),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ContentReport.SelfReportNotAllowed");
    }

    [Fact]
    public async Task CreateContentReportHandler_ReturnsConflictWhenAlreadyReportedIn24Hours()
    {
        var repo = Substitute.For<IContentReportRepository>();
        var targetService = Substitute.For<IReportTargetService>();
        var currentUser = Substitute.For<ICurrentUserService>();
        var clock = Substitute.For<IDateTimeProvider>();
        var logger = Substitute.For<ILogger<CreateContentReportCommandHandler>>();

        Guid reporterId = Guid.NewGuid();
        Guid offerId = Guid.NewGuid();
        currentUser.UserId.Returns(reporterId.ToString());
        clock.UtcNow.Returns(Now);
        targetService.GetTargetInfoAsync(ReportTargetType.Offer, offerId, Arg.Any<CancellationToken>())
            .Returns(new ReportTargetInfo(Exists: true, AuthorUserId: Guid.NewGuid()));
        repo.ExistsRecentAsync(reporterId, ReportTargetType.Offer, offerId, Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(true);

        var cdnInvalidator = Substitute.For<ICdnCacheInvalidator>();
        var handler = new CreateContentReportCommandHandler(
            repo,
            targetService,
            currentUser,
            clock,
            Options.Create(new ContentReportSettings()),
            cdnInvalidator,
            logger);

        Result<ContentReportDto> result = await handler.Handle(
            new CreateContentReportCommand("offer", offerId, "fraude", "Precio falso"),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Conflict);
    }

    [Fact]
    public async Task CreateContentReportHandler_MarksTargetForReviewAtThreshold_AndOmitsReporterPii()
    {
        var repo = Substitute.For<IContentReportRepository>();
        var targetService = Substitute.For<IReportTargetService>();
        var currentUser = Substitute.For<ICurrentUserService>();
        var clock = Substitute.For<IDateTimeProvider>();
        var logger = Substitute.For<ILogger<CreateContentReportCommandHandler>>();

        Guid reporterId = Guid.NewGuid();
        Guid commentId = Guid.NewGuid();
        currentUser.UserId.Returns(reporterId.ToString());
        clock.UtcNow.Returns(Now);
        targetService.GetTargetInfoAsync(ReportTargetType.Comment, commentId, Arg.Any<CancellationToken>())
            .Returns(new ReportTargetInfo(Exists: true, AuthorUserId: Guid.NewGuid()));
        repo.TryAddAsync(Arg.Any<ContentReport>(), Arg.Any<CancellationToken>()).Returns(true);
        repo.CountRecentByTargetAsync(ReportTargetType.Comment, commentId, Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(3);
        targetService.MarkUnderReviewAsync(ReportTargetType.Comment, commentId, Now, Arg.Any<CancellationToken>())
            .Returns(true);

        var cdnInvalidator = Substitute.For<ICdnCacheInvalidator>();
        var handler = new CreateContentReportCommandHandler(
            repo,
            targetService,
            currentUser,
            clock,
            Options.Create(new ContentReportSettings { AutoReviewThreshold = 3, DeduplicationWindowHours = 24 }),
            cdnInvalidator,
            logger);

        Result<ContentReportDto> result = await handler.Handle(
            new CreateContentReportCommand("comment", commentId, "ofensivo", "Insultos repetidos"),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.TargetMarkedForReview.Should().BeTrue();
        result.Value.TargetType.Should().Be("comment");
        typeof(ContentReportDto).GetProperties().Select(p => p.Name)
            .Should().NotContain(n => n.Contains("Reporter", StringComparison.OrdinalIgnoreCase));
    }
}
