using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Common.Models;
using Sensus.Application.Feed.Commands.CreateFeedComment;
using Sensus.Application.Feed.DTOs;
using Sensus.Application.Feed.Queries.GetPostComments;
using Sensus.Domain.Common;
using Sensus.Domain.Entities;
using Sensus.Domain.Repositories;

namespace Sensus.Application.UnitTests.Feed;

public sealed class FeedCommentsTests
{
    private static readonly DateTime Now = new(2026, 11, 21, 14, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void FeedCommentCreate_FailsWhenEmptyOrExceedsMaxLength()
    {
        Result<FeedComment> empty = FeedComment.Create(Guid.NewGuid(), Guid.NewGuid(), "Ana", "   ", Now);
        empty.IsFailure.Should().BeTrue();
        empty.Error.Code.Should().Be("FeedComment.EmptyText");

        string tooLong = new('a', FeedComment.MaxTextLength + 1);
        Result<FeedComment> overLimit = FeedComment.Create(Guid.NewGuid(), Guid.NewGuid(), "Ana", tooLong, Now);
        overLimit.IsFailure.Should().BeTrue();
        overLimit.Error.Code.Should().Be("FeedComment.TextTooLong");
    }

    [Fact]
    public void FeedCommentCreate_UsesDefaultDisplayName_WhenBlank()
    {
        Result<FeedComment> result = FeedComment.Create(Guid.NewGuid(), Guid.NewGuid(), "   ", "Buena oferta!", Now);
        result.IsSuccess.Should().BeTrue();
        result.Value.AuthorDisplayName.Should().Be(FeedComment.DefaultAuthorDisplayName);
        result.Value.Text.Should().Be("Buena oferta!");
    }

    [Fact]
    public async Task CreateFeedCommentHandler_ReturnsNotFound_WhenPostDoesNotExistOrIsHidden()
    {
        var commentRepo = Substitute.For<IFeedCommentRepository>();
        var postLookup = Substitute.For<IFeedPostVisibilityLookup>();
        var userRepo = Substitute.For<IUserRepository>();
        var currentUser = Substitute.For<ICurrentUserService>();
        var clock = Substitute.For<IDateTimeProvider>();

        Guid userId = Guid.NewGuid();
        Guid postId = Guid.NewGuid();
        currentUser.UserId.Returns(userId.ToString());
        clock.UtcNow.Returns(Now);
        postLookup.GetVisibilityAsync(postId, Arg.Any<CancellationToken>())
            .Returns(new FeedPostLookupResult(Exists: true, IsVisible: false, AuthorUserId: Guid.NewGuid()));

        var handler = new CreateFeedCommentCommandHandler(
            commentRepo,
            postLookup,
            userRepo,
            currentUser,
            clock,
            NullLogger<CreateFeedCommentCommandHandler>.Instance,
            Substitute.For<IModerationAuditRecorder>());

        Result<FeedCommentDto> result = await handler.Handle(new CreateFeedCommentCommand(postId, "Me interesa"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
        await commentRepo.DidNotReceive().AddAsync(Arg.Any<FeedComment>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateFeedCommentHandler_RejectsProfanityAndDoesNotPersist()
    {
        var commentRepo = Substitute.For<IFeedCommentRepository>();
        var postLookup = Substitute.For<IFeedPostVisibilityLookup>();
        var userRepo = Substitute.For<IUserRepository>();
        var currentUser = Substitute.For<ICurrentUserService>();
        var clock = Substitute.For<IDateTimeProvider>();

        Guid userId = Guid.NewGuid();
        Guid postId = Guid.NewGuid();
        currentUser.UserId.Returns(userId.ToString());
        clock.UtcNow.Returns(Now);
        postLookup.GetVisibilityAsync(postId, Arg.Any<CancellationToken>())
            .Returns(new FeedPostLookupResult(Exists: true, IsVisible: true, AuthorUserId: Guid.NewGuid()));

        var handler = new CreateFeedCommentCommandHandler(
            commentRepo,
            postLookup,
            userRepo,
            currentUser,
            clock,
            NullLogger<CreateFeedCommentCommandHandler>.Instance,
            Substitute.For<IModerationAuditRecorder>());

        Result<FeedCommentDto> result = await handler.Handle(
            new CreateFeedCommentCommand(postId, "Esto es una mierda total"),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("FeedComment.ModerationRejected");
        await commentRepo.DidNotReceive().AddAsync(Arg.Any<FeedComment>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateFeedCommentHandler_PersistsAndReturnsDto_WhenApproved()
    {
        var commentRepo = Substitute.For<IFeedCommentRepository>();
        var postLookup = Substitute.For<IFeedPostVisibilityLookup>();
        var userRepo = Substitute.For<IUserRepository>();
        var currentUser = Substitute.For<ICurrentUserService>();
        var clock = Substitute.For<IDateTimeProvider>();
        var moderation = Substitute.For<ITextModerationService>();

        User author = User.Create("Sofía Ruiz", "sofia@sensus.com", "hash", true, Now).Value;
        Guid postId = Guid.NewGuid();
        currentUser.UserId.Returns(author.Id.ToString());
        clock.UtcNow.Returns(Now);
        userRepo.GetByIdAsync(author.Id, Arg.Any<CancellationToken>()).Returns(author);
        postLookup.GetVisibilityAsync(postId, Arg.Any<CancellationToken>())
            .Returns(new FeedPostLookupResult(Exists: true, IsVisible: true, AuthorUserId: Guid.NewGuid()));
        moderation.ModerateContentTextAsync(Arg.Any<string>(), "feed_comment", Arg.Any<CancellationToken>())
            .Returns(new TextModerationResult(true, Decision: "Approved"));

        var auditRecorder = Substitute.For<IModerationAuditRecorder>();
        auditRecorder.RecordAsync(Arg.Any<ModerationAuditEntry>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        var handler = new CreateFeedCommentCommandHandler(
            commentRepo,
            postLookup,
            userRepo,
            currentUser,
            clock,
            NullLogger<CreateFeedCommentCommandHandler>.Instance,
            auditRecorder,
            moderation);

        Result<FeedCommentDto> result = await handler.Handle(
            new CreateFeedCommentCommand(postId, "  Excelente promoción cerca!  "),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.PostId.Should().Be(postId);
        result.Value.AuthorName.Should().Be("Sofía Ruiz");
        result.Value.Content.Should().Be("Excelente promoción cerca!");
        await commentRepo.Received(1).AddAsync(Arg.Is<FeedComment>(c => c.PostId == postId), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetPostCommentsHandler_ReturnsPagedCommentsAndValidatesRange()
    {
        var commentRepo = Substitute.For<IFeedCommentRepository>();
        var postLookup = Substitute.For<IFeedPostVisibilityLookup>();
        Guid postId = Guid.NewGuid();
        Guid userId = Guid.NewGuid();

        postLookup.GetVisibilityAsync(postId, Arg.Any<CancellationToken>())
            .Returns(new FeedPostLookupResult(Exists: true, IsVisible: true, AuthorUserId: Guid.NewGuid()));

        List<FeedComment> items =
        [
            FeedComment.Create(postId, userId, "Ana", "Primero", Now.AddMinutes(1)).Value,
            FeedComment.Create(postId, userId, "Luis", "Segundo", Now.AddMinutes(2)).Value
        ];
        commentRepo.GetByPostIdPagedAsync(postId, 1, 20, Arg.Any<CancellationToken>()).Returns(items);
        commentRepo.CountByPostIdAsync(postId, Arg.Any<CancellationToken>()).Returns(2);

        var handler = new GetPostCommentsQueryHandler(commentRepo, postLookup);
        Result<PagedResult<FeedCommentDto>> result = await handler.Handle(new GetPostCommentsQuery(postId, 1, 20), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.TotalCount.Should().Be(2);
        result.Value.Items.Select(i => i.Content).Should().Equal("Primero", "Segundo");

        var validator = new GetPostCommentsQueryValidator();
        (await validator.ValidateAsync(new GetPostCommentsQuery(postId, 1, 101))).IsValid.Should().BeFalse();
        (await validator.ValidateAsync(new GetPostCommentsQuery(postId, 0, 20))).IsValid.Should().BeFalse();
    }
}
