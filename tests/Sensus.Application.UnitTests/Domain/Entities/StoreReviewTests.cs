using FluentAssertions;
using Sensus.Domain.Common;
using Sensus.Domain.Entities;
using Xunit;

namespace Sensus.Application.UnitTests.Domain.Entities;

public sealed class StoreReviewTests
{
    private static readonly DateTime Now = new(2026, 11, 22, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Create_WithValidParameters_ReturnsSuccess()
    {
        Guid merchantId = Guid.NewGuid();
        Guid authorId = Guid.NewGuid();

        Result<StoreReview> result = StoreReview.Create(
            merchantId,
            authorId,
            "Juan Pérez",
            "https://photo.url",
            5,
            "Excelente servicio y atención.",
            Now);

        result.IsSuccess.Should().BeTrue();
        StoreReview review = result.Value;
        review.Id.Should().NotBe(Guid.Empty);
        review.MerchantId.Should().Be(merchantId);
        review.AuthorUserId.Should().Be(authorId);
        review.AuthorName.Should().Be("Juan Pérez");
        review.AuthorPhotoUrl.Should().Be("https://photo.url");
        review.Rating.Should().Be(5);
        review.Comment.Should().Be("Excelente servicio y atención.");
        review.CreatedAtUtc.Should().Be(Now);
        review.LikedByUserIds.Should().BeEmpty();
        review.Replies.Should().BeEmpty();
    }

    [Fact]
    public void Create_WhenAuthorNameIsBlank_UsesDefaultName()
    {
        Result<StoreReview> result = StoreReview.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "   ",
            null,
            4,
            "Buen lugar.",
            Now);

        result.IsSuccess.Should().BeTrue();
        result.Value.AuthorName.Should().Be("Usuario Sensus");
    }

    [Fact]
    public void Create_WhenMerchantIdIsEmpty_ReturnsValidationError()
    {
        Result<StoreReview> result = StoreReview.Create(
            Guid.Empty,
            Guid.NewGuid(),
            "Juan",
            null,
            5,
            "Comentario",
            Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("StoreReview.MerchantRequired");
    }

    [Fact]
    public void Create_WhenAuthorUserIdIsEmpty_ReturnsValidationError()
    {
        Result<StoreReview> result = StoreReview.Create(
            Guid.NewGuid(),
            Guid.Empty,
            "Juan",
            null,
            5,
            "Comentario",
            Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("StoreReview.AuthorRequired");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(6)]
    [InlineData(10)]
    public void Create_WhenRatingIsOutOfRange_ReturnsValidationError(int rating)
    {
        Result<StoreReview> result = StoreReview.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Juan",
            null,
            rating,
            "Comentario",
            Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("StoreReview.RatingInvalid");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WhenCommentIsBlank_ReturnsValidationError(string? comment)
    {
        Result<StoreReview> result = StoreReview.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Juan",
            null,
            5,
            comment!,
            Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("StoreReview.CommentInvalid");
    }

    [Fact]
    public void Create_WhenCommentExceeds600Chars_ReturnsValidationError()
    {
        string longComment = new('a', 601);
        Result<StoreReview> result = StoreReview.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Juan",
            null,
            5,
            longComment,
            Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("StoreReview.CommentInvalid");
    }

    [Fact]
    public void ToggleLike_TogglesLikeStateCorrectly()
    {
        var review = StoreReview.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Juan",
            null,
            5,
            "Comentario",
            Now).Value;

        Guid userId = Guid.NewGuid();

        // Primer like
        bool liked = review.ToggleLike(userId);
        liked.Should().BeTrue();
        review.LikedByUserIds.Should().Contain(userId);

        // Segundo like (remueve)
        bool unliked = review.ToggleLike(userId);
        unliked.Should().BeFalse();
        review.LikedByUserIds.Should().NotContain(userId);

        // UserId vacío no altera nada
        review.ToggleLike(Guid.Empty).Should().BeFalse();
    }

    [Fact]
    public void AddReply_WithValidComment_AddsReplySuccessfully()
    {
        var review = StoreReview.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Juan",
            null,
            5,
            "Comentario",
            Now).Value;

        Guid replyAuthorId = Guid.NewGuid();
        Result<StoreReviewReply> result = review.AddReply(
            replyAuthorId,
            "Comerciante",
            "https://photo.url",
            "¡Muchas gracias por tu visita!",
            Now.AddHours(1));

        result.IsSuccess.Should().BeTrue();
        review.Replies.Should().HaveCount(1);
        StoreReviewReply reply = review.Replies.First();
        reply.AuthorUserId.Should().Be(replyAuthorId);
        reply.AuthorName.Should().Be("Comerciante");
        reply.Comment.Should().Be("¡Muchas gracias por tu visita!");
    }

    [Fact]
    public void AddReply_WhenInvalid_ReturnsFailure()
    {
        var review = StoreReview.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Juan",
            null,
            5,
            "Comentario",
            Now).Value;

        Result<StoreReviewReply> result = review.AddReply(
            Guid.Empty,
            "Comerciante",
            null,
            "Respuesta",
            Now);

        result.IsFailure.Should().BeTrue();
        review.Replies.Should().BeEmpty();
    }

    [Fact]
    public void StoreReviewReply_ValidationChecks()
    {
        // Autor vacío
        var r1 = StoreReviewReply.Create(Guid.Empty, "Autor", null, "Comentario", Now);
        r1.IsFailure.Should().BeTrue();
        r1.Error.Code.Should().Be("StoreReviewReply.AuthorRequired");

        // Comentario vacío
        var r2 = StoreReviewReply.Create(Guid.NewGuid(), "Autor", null, "   ", Now);
        r2.IsFailure.Should().BeTrue();
        r2.Error.Code.Should().Be("StoreReviewReply.CommentInvalid");

        // Comentario demasiado largo (>500)
        var r3 = StoreReviewReply.Create(Guid.NewGuid(), "Autor", null, new string('x', 501), Now);
        r3.IsFailure.Should().BeTrue();

        // Nombre vacío usa fallback
        var r4 = StoreReviewReply.Create(Guid.NewGuid(), "   ", null, "Válido", Now);
        r4.IsSuccess.Should().BeTrue();
        r4.Value.AuthorName.Should().Be("Usuario Sensus");
    }

    [Fact]
    public void Rehydrate_ReconstructsReviewAndRepliesAndLikes()
    {
        Guid id = Guid.NewGuid();
        Guid merchantId = Guid.NewGuid();
        Guid authorId = Guid.NewGuid();
        Guid likerId = Guid.NewGuid();

        var reply = StoreReviewReply.Rehydrate(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Dueño",
            null,
            "Gracias",
            Now.AddHours(2));

        StoreReview review = StoreReview.Rehydrate(
            id,
            merchantId,
            authorId,
            "Cliente",
            "https://photo",
            4,
            "Buen lugar",
            Now,
            [likerId, Guid.Empty, likerId],
            [reply]);

        review.Id.Should().Be(id);
        review.MerchantId.Should().Be(merchantId);
        review.AuthorUserId.Should().Be(authorId);
        review.AuthorName.Should().Be("Cliente");
        review.LikedByUserIds.Should().ContainSingle().Which.Should().Be(likerId);
        review.Replies.Should().ContainSingle().Which.Comment.Should().Be("Gracias");
    }
}
