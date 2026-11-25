using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Feed.Commands.SetFeedPostLike;
using Sensus.Application.Feed.DTOs;
using Sensus.Domain.Common;
using Sensus.Domain.Entities;
using Sensus.Domain.Repositories;

namespace Sensus.Application.UnitTests.Feed;

/// <summary>
/// Spec 039: me gusta en publicaciones (FR-039-01 a FR-039-05, NFR-039-02).
/// </summary>
public sealed class FeedPostLikesTests
{
    private static readonly DateTime Now = new(2026, 11, 25, 10, 0, 0, DateTimeKind.Utc);

    private readonly IFeedPostVisibilityLookup _lookup = Substitute.For<IFeedPostVisibilityLookup>();
    private readonly IFeedPostLikeRepository _likes = Substitute.For<IFeedPostLikeRepository>();
    private readonly ICurrentUserService _currentUser = Substitute.For<ICurrentUserService>();
    private readonly IDateTimeProvider _clock = Substitute.For<IDateTimeProvider>();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _postId = Guid.NewGuid();

    public FeedPostLikesTests()
    {
        _clock.UtcNow.Returns(Now);
        _currentUser.UserId.Returns(_userId.ToString());
        _currentUser.IsGuest.Returns(false);
        _lookup.GetVisibilityAsync(_postId, Arg.Any<CancellationToken>())
            .Returns(new FeedPostLookupResult(Exists: true, IsVisible: true, AuthorUserId: Guid.NewGuid()));
    }

    private SetFeedPostLikeCommandHandler CreateHandler() =>
        new(_lookup, _likes, _currentUser, _clock, NullLogger<SetFeedPostLikeCommandHandler>.Instance);

    [Fact]
    public void Create_FailsWhenPostOrUserIsEmpty()
    {
        FeedPostLike.Create(Guid.Empty, _userId, Now).Error.Code.Should().Be("FeedPostLike.PostRequired");
        FeedPostLike.Create(_postId, Guid.Empty, Now).Error.Code.Should().Be("FeedPostLike.UserRequired");
    }

    [Fact]
    public void Create_SetsPostUserAndUtcDate()
    {
        Result<FeedPostLike> result = FeedPostLike.Create(_postId, _userId, Now);

        result.IsSuccess.Should().BeTrue();
        result.Value.PostId.Should().Be(_postId);
        result.Value.UserId.Should().Be(_userId);
        result.Value.CreatedAtUtc.Should().Be(Now);
        result.Value.CreatedAtUtc.Kind.Should().Be(DateTimeKind.Utc);

        FeedPostLike rehydrated = FeedPostLike.Rehydrate(result.Value.Id, _postId, _userId, Now);
        rehydrated.Id.Should().Be(result.Value.Id);
    }

    [Theory]
    [InlineData(-5L, 0)]
    [InlineData(0L, 0)]
    [InlineData(7L, 7)]
    [InlineData(long.MaxValue, int.MaxValue)]
    public void ClampCount_NeverReturnsNegative(long raw, int expected)
    {
        FeedPostLike.ClampCount(raw).Should().Be(expected);
    }

    [Fact]
    public async Task Like_AddsRecordAndReturnsLikedWithUpdatedCount()
    {
        _likes.AddAsync(Arg.Any<FeedPostLike>(), Arg.Any<CancellationToken>()).Returns(true);
        _likes.CountByPostIdAsync(_postId, Arg.Any<CancellationToken>()).Returns(4);

        Result<FeedPostLikeStatusDto> result = await CreateHandler().Handle(
            new SetFeedPostLikeCommand(_postId, Liked: true), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(new FeedPostLikeStatusDto(true, 4));
        await _likes.Received(1).AddAsync(
            Arg.Is<FeedPostLike>(l => l.PostId == _postId && l.UserId == _userId),
            Arg.Any<CancellationToken>());
        await _likes.DidNotReceive().RemoveAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RepeatedLike_IsIdempotentAndKeepsLikedTrue()
    {
        // CA-039-04: la segunda solicitud con la misma intención no crea otro registro.
        _likes.AddAsync(Arg.Any<FeedPostLike>(), Arg.Any<CancellationToken>()).Returns(false);
        _likes.CountByPostIdAsync(_postId, Arg.Any<CancellationToken>()).Returns(1);

        Result<FeedPostLikeStatusDto> result = await CreateHandler().Handle(
            new SetFeedPostLikeCommand(_postId, Liked: true), CancellationToken.None);

        result.Value.Should().Be(new FeedPostLikeStatusDto(true, 1));
    }

    [Fact]
    public async Task Unlike_RemovesRecordAndReturnsLikedFalse()
    {
        _likes.RemoveAsync(_postId, _userId, Arg.Any<CancellationToken>()).Returns(true);
        _likes.CountByPostIdAsync(_postId, Arg.Any<CancellationToken>()).Returns(0);

        Result<FeedPostLikeStatusDto> result = await CreateHandler().Handle(
            new SetFeedPostLikeCommand(_postId, Liked: false), CancellationToken.None);

        result.Value.Should().Be(new FeedPostLikeStatusDto(false, 0));
        await _likes.DidNotReceive().AddAsync(Arg.Any<FeedPostLike>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Count_IsClampedToZero_WhenStoreReportsNegative()
    {
        _likes.RemoveAsync(_postId, _userId, Arg.Any<CancellationToken>()).Returns(false);
        _likes.CountByPostIdAsync(_postId, Arg.Any<CancellationToken>()).Returns(-1);

        Result<FeedPostLikeStatusDto> result = await CreateHandler().Handle(
            new SetFeedPostLikeCommand(_postId, Liked: false), CancellationToken.None);

        result.Value.LikeCount.Should().Be(0);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    public async Task ReturnsNotFound_WhenPostIsMissingOrNotVisible(bool exists, bool visible)
    {
        // FR-039-03 / CA-039-03: inexistente, en cuarentena o eliminada → 404 uniforme.
        _lookup.GetVisibilityAsync(_postId, Arg.Any<CancellationToken>())
            .Returns(new FeedPostLookupResult(exists, visible, null));

        Result<FeedPostLikeStatusDto> result = await CreateHandler().Handle(
            new SetFeedPostLikeCommand(_postId, Liked: true), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
        await _likes.DidNotReceive().AddAsync(Arg.Any<FeedPostLike>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReturnsUnauthorized_ForGuestOrMissingUser()
    {
        _currentUser.IsGuest.Returns(true);
        Result<FeedPostLikeStatusDto> guest = await CreateHandler().Handle(
            new SetFeedPostLikeCommand(_postId, true), CancellationToken.None);
        guest.Error.Code.Should().Be("FeedPostLike.Unauthorized");

        _currentUser.IsGuest.Returns(false);
        _currentUser.UserId.Returns((string?)null);
        Result<FeedPostLikeStatusDto> anonymous = await CreateHandler().Handle(
            new SetFeedPostLikeCommand(_postId, true), CancellationToken.None);
        anonymous.Error.Code.Should().Be("FeedPostLike.Unauthorized");

        await _lookup.DidNotReceive().GetVisibilityAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Validator_RejectsEmptyPostId()
    {
        var validator = new SetFeedPostLikeCommandValidator();

        validator.Validate(new SetFeedPostLikeCommand(Guid.Empty, true)).IsValid.Should().BeFalse();
        validator.Validate(new SetFeedPostLikeCommand(_postId, false)).IsValid.Should().BeTrue();
    }
}
