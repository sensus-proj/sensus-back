using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Feed.Commands.CreateFeedPost;
using Sensus.Application.Feed.Commands.DeleteFeedPost;
using Sensus.Application.Feed.DTOs;
using Sensus.Domain.Common;
using Sensus.Domain.Entities;
using Sensus.Domain.Enums;
using Sensus.Domain.Repositories;

namespace Sensus.Application.UnitTests.Feed;

public sealed class FeedPostsTests
{
    private static readonly DateTime Now = new(2026, 11, 22, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void FeedPostCreate_FailsWhenAuthorIsEmpty()
    {
        Result<FeedPost> result = FeedPost.Create(Guid.Empty, "Carlos", "Hola mundo", null, Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("FeedPost.InvalidAuthor");
    }

    [Fact]
    public void FeedPostCreate_FailsWhenContentIsEmptyOrExceeds500Chars()
    {
        Result<FeedPost> empty = FeedPost.Create(Guid.NewGuid(), "Carlos", "   ", null, Now);
        empty.IsFailure.Should().BeTrue();
        empty.Error.Code.Should().Be("FeedPost.EmptyContent");

        string tooLong = new('x', FeedPost.MaxContentLength + 1);
        Result<FeedPost> overLimit = FeedPost.Create(Guid.NewGuid(), "Carlos", tooLong, null, Now);
        overLimit.IsFailure.Should().BeTrue();
        overLimit.Error.Code.Should().Be("FeedPost.ContentLengthOutOfRange");
    }

    [Fact]
    public void FeedPostCreate_SucceedsAndSetsDefaults()
    {
        Guid authorId = Guid.NewGuid();
        Result<FeedPost> result = FeedPost.Create(
            authorId,
            "  ",
            "  Contenido genial de prueba  ",
            "https://res.cloudinary.com/sensus/image/upload/sample.jpg",
            Now);

        result.IsSuccess.Should().BeTrue();
        FeedPost post = result.Value;
        post.AuthorUserId.Should().Be(authorId);
        post.AuthorDisplayName.Should().Be(FeedPost.DefaultAuthorDisplayName);
        post.Content.Should().Be("Contenido genial de prueba");
        post.ImageUrl.Should().Be("https://res.cloudinary.com/sensus/image/upload/sample.jpg");
        post.Status.Should().Be(FeedPostStatus.Published);
        post.IsDeleted.Should().BeFalse();
        post.CreatedAtUtc.Should().Be(Now);
    }

    [Fact]
    public void FeedPostDelete_EnforcesAuthorOrAdminAuthorization()
    {
        Guid authorId = Guid.NewGuid();
        Guid otherUserId = Guid.NewGuid();
        Guid adminId = Guid.NewGuid();

        FeedPost post = FeedPost.Create(authorId, "Autor", "Post", null, Now).Value;

        // Otro usuario no admin: prohibido
        Result forbidden = post.Delete(otherUserId, isAdmin: false, Now.AddMinutes(10));
        forbidden.IsFailure.Should().BeTrue();
        forbidden.Error.Code.Should().Be("FeedPost.ForbiddenDelete");
        post.IsDeleted.Should().BeFalse();

        // Admin: permitido
        Result adminDelete = post.Delete(adminId, isAdmin: true, Now.AddMinutes(15));
        adminDelete.IsSuccess.Should().BeTrue();
        post.IsDeleted.Should().BeTrue();
        post.Status.Should().Be(FeedPostStatus.Deleted);
        post.DeletedAtUtc.Should().Be(Now.AddMinutes(15));
    }

    [Fact]
    public async Task CreateFeedPostHandler_ReturnsUnauthorized_WhenNoUserLoggedIn()
    {
        var postRepo = Substitute.For<IFeedPostRepository>();
        var userRepo = Substitute.For<IUserRepository>();
        var currentUser = Substitute.For<ICurrentUserService>();
        var clock = Substitute.For<IDateTimeProvider>();

        currentUser.UserId.Returns((string?)null);

        var handler = new CreateFeedPostCommandHandler(
            postRepo,
            userRepo,
            currentUser,
            clock,
            NullLogger<CreateFeedPostCommandHandler>.Instance);

        Result<FeedPostDto> result = await handler.Handle(
            new CreateFeedPostCommand("Contenido"),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("FeedPost.Unauthorized");
    }

    [Fact]
    public async Task CreateFeedPostHandler_FailsClosed_WhenModerationRejectsText()
    {
        var postRepo = Substitute.For<IFeedPostRepository>();
        var userRepo = Substitute.For<IUserRepository>();
        var currentUser = Substitute.For<ICurrentUserService>();
        var clock = Substitute.For<IDateTimeProvider>();
        var textModeration = Substitute.For<ITextModerationService>();

        Guid userId = Guid.NewGuid();
        currentUser.UserId.Returns(userId.ToString());
        clock.UtcNow.Returns(Now);

        textModeration.ModerateContentTextAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new TextModerationResult(IsApproved: false, Message: "Contenido inapropiado"));

        var handler = new CreateFeedPostCommandHandler(
            postRepo,
            userRepo,
            currentUser,
            clock,
            NullLogger<CreateFeedPostCommandHandler>.Instance,
            textModerationService: textModeration);

        Result<FeedPostDto> result = await handler.Handle(
            new CreateFeedPostCommand("Contenido tóxico"),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("FeedPost.ModerationRejected");
        await postRepo.DidNotReceive().AddAsync(Arg.Any<FeedPost>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateFeedPostHandler_CreatesAndSavesPost_WhenContentIsApproved()
    {
        var postRepo = Substitute.For<IFeedPostRepository>();
        var userRepo = Substitute.For<IUserRepository>();
        var currentUser = Substitute.For<ICurrentUserService>();
        var clock = Substitute.For<IDateTimeProvider>();
        var textModeration = Substitute.For<ITextModerationService>();
        var auditRecorder = Substitute.For<IModerationAuditRecorder>();
        auditRecorder.RecordAsync(Arg.Any<ModerationAuditEntry>(), Arg.Any<CancellationToken>()).Returns(Result.Success());

        Guid userId = Guid.NewGuid();
        currentUser.UserId.Returns(userId.ToString());
        clock.UtcNow.Returns(Now);

        textModeration.ModerateContentTextAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new TextModerationResult(IsApproved: true));

        var handler = new CreateFeedPostCommandHandler(
            postRepo,
            userRepo,
            currentUser,
            clock,
            NullLogger<CreateFeedPostCommandHandler>.Instance,
            textModerationService: textModeration,
            auditRecorder: auditRecorder);

        Result<FeedPostDto> result = await handler.Handle(
            new CreateFeedPostCommand("Mi primera publicación"),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Content.Should().Be("Mi primera publicación");
        result.Value.AuthorUserId.Should().Be(userId);
        result.Value.Status.Should().Be(FeedPostStatus.Published.ToString());

        await postRepo.Received(1).AddAsync(Arg.Is<FeedPost>(p => p.Content == "Mi primera publicación"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteFeedPostHandler_ReturnsNotFound_WhenPostDoesNotExist()
    {
        var postRepo = Substitute.For<IFeedPostRepository>();
        var currentUser = Substitute.For<ICurrentUserService>();
        var clock = Substitute.For<IDateTimeProvider>();

        currentUser.UserId.Returns(Guid.NewGuid().ToString());
        currentUser.Roles.Returns(new List<string> { "Consumidor" });
        postRepo.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((FeedPost?)null);

        var handler = new DeleteFeedPostCommandHandler(
            postRepo,
            currentUser,
            clock,
            NullLogger<DeleteFeedPostCommandHandler>.Instance);

        Result result = await handler.Handle(
            new DeleteFeedPostCommand(Guid.NewGuid()),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("FeedPost.NotFound");
    }

    [Fact]
    public async Task DeleteFeedPostHandler_DeletesSuccessfully_WhenUserIsAuthor()
    {
        var postRepo = Substitute.For<IFeedPostRepository>();
        var currentUser = Substitute.For<ICurrentUserService>();
        var clock = Substitute.For<IDateTimeProvider>();

        Guid authorId = Guid.NewGuid();
        Guid postId = Guid.NewGuid();

        currentUser.UserId.Returns(authorId.ToString());
        currentUser.Roles.Returns(new List<string> { "Consumidor" });
        clock.UtcNow.Returns(Now);

        FeedPost post = FeedPost.Create(authorId, "Autor", "Post a borrar", null, Now, id: postId).Value;
        postRepo.GetByIdAsync(postId, Arg.Any<CancellationToken>()).Returns(post);

        var handler = new DeleteFeedPostCommandHandler(
            postRepo,
            currentUser,
            clock,
            NullLogger<DeleteFeedPostCommandHandler>.Instance);

        Result result = await handler.Handle(new DeleteFeedPostCommand(postId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        post.IsDeleted.Should().BeTrue();
        await postRepo.Received(1).UpdateAsync(post, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateFeedPostHandler_QuarantinesPost_WhenTextModerationIsNotRegistered()
    {
        var postRepo = Substitute.For<IFeedPostRepository>();
        var userRepo = Substitute.For<IUserRepository>();
        var currentUser = Substitute.For<ICurrentUserService>();
        var clock = Substitute.For<IDateTimeProvider>();
        var auditRecorder = Substitute.For<IModerationAuditRecorder>();
        var imageModeration = Substitute.For<IImageModerationService>();
        var mediaStorage = Substitute.For<IMediaStorage>();

        Guid userId = Guid.NewGuid();
        currentUser.UserId.Returns(userId.ToString());
        clock.UtcNow.Returns(Now);

        var handler = new CreateFeedPostCommandHandler(
            postRepo,
            userRepo,
            currentUser,
            clock,
            NullLogger<CreateFeedPostCommandHandler>.Instance,
            textModerationService: null,
            imageModerationService: imageModeration,
            mediaStorage: mediaStorage,
            auditRecorder: auditRecorder);

        Result<FeedPostDto> result = await handler.Handle(
            new CreateFeedPostCommand("Contenido sin moderador", null, [1, 2, 3], "image/jpeg"),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(FeedPostStatus.Quarantined.ToString());
        result.Value.ImageUrl.Should().BeNull();
        await postRepo.Received(1).AddAsync(
            Arg.Is<FeedPost>(p =>
                p.Status == FeedPostStatus.Quarantined
                && p.ImageUrl == null
                && p.ImagePublicId == null),
            Arg.Any<CancellationToken>());
        await imageModeration.DidNotReceive().ModerateImageBytesAsync(
            Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
        await mediaStorage.DidNotReceive().UploadImageAsync(
            Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await auditRecorder.Received(1).RecordBestEffortAsync(
            Arg.Is<ModerationAuditEntry>(e =>
                e.TargetType == ModerationTargetType.FeedPost
                && e.Verdict == ModerationVerdict.Quarantined),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateFeedPostHandler_QuarantinesPostWithoutUpload_WhenImageModerationUnavailableOrQuarantined()
    {
        var postRepo = Substitute.For<IFeedPostRepository>();
        var userRepo = Substitute.For<IUserRepository>();
        var currentUser = Substitute.For<ICurrentUserService>();
        var clock = Substitute.For<IDateTimeProvider>();
        var textModeration = Substitute.For<ITextModerationService>();
        var imageModeration = Substitute.For<IImageModerationService>();
        var mediaStorage = Substitute.For<IMediaStorage>();
        var auditRecorder = Substitute.For<IModerationAuditRecorder>();

        Guid userId = Guid.NewGuid();
        currentUser.UserId.Returns(userId.ToString());
        clock.UtcNow.Returns(Now);
        textModeration.ModerateContentTextAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new TextModerationResult(true, Decision: "Approved"));
        imageModeration.ModerateImageBytesAsync(Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(new ImageModerationResult(false, "Timeout", null, "Quarantine"));

        var handler = new CreateFeedPostCommandHandler(
            postRepo,
            userRepo,
            currentUser,
            clock,
            NullLogger<CreateFeedPostCommandHandler>.Instance,
            textModerationService: textModeration,
            imageModerationService: imageModeration,
            mediaStorage: mediaStorage,
            auditRecorder: auditRecorder);

        Result<FeedPostDto> result = await handler.Handle(
            new CreateFeedPostCommand("Publicación con imagen fallida", null, [1, 2, 3], "image/jpeg"),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(FeedPostStatus.Quarantined.ToString());
        result.Value.ImageUrl.Should().BeNull();
        await mediaStorage.DidNotReceive().UploadImageAsync(
            Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await postRepo.Received(1).AddAsync(
            Arg.Is<FeedPost>(p => p.Status == FeedPostStatus.Quarantined && p.ImageUrl == null && p.ImagePublicId == null),
            Arg.Any<CancellationToken>());
        await auditRecorder.Received(1).RecordBestEffortAsync(
            Arg.Is<ModerationAuditEntry>(e =>
                e.TargetType == ModerationTargetType.FeedPost
                && e.Verdict == ModerationVerdict.Quarantined),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateFeedPostHandler_FailsClosed_WhenAuditRecorderFailsOnPublish()
    {
        var postRepo = Substitute.For<IFeedPostRepository>();
        var userRepo = Substitute.For<IUserRepository>();
        var currentUser = Substitute.For<ICurrentUserService>();
        var clock = Substitute.For<IDateTimeProvider>();
        var textModeration = Substitute.For<ITextModerationService>();
        var auditRecorder = Substitute.For<IModerationAuditRecorder>();

        currentUser.UserId.Returns(Guid.NewGuid().ToString());
        clock.UtcNow.Returns(Now);
        textModeration.ModerateContentTextAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new TextModerationResult(true, Decision: "Approved"));
        auditRecorder.RecordAsync(Arg.Any<ModerationAuditEntry>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure(Error.Unavailable("ModerationAudit.Unavailable", "Sin auditoría")));

        var handler = new CreateFeedPostCommandHandler(
            postRepo,
            userRepo,
            currentUser,
            clock,
            NullLogger<CreateFeedPostCommandHandler>.Instance,
            textModerationService: textModeration,
            auditRecorder: auditRecorder);

        Result<FeedPostDto> result = await handler.Handle(
            new CreateFeedPostCommand("Texto aprobado sin auditoría"),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ModerationAudit.Unavailable");
        await postRepo.DidNotReceive().AddAsync(Arg.Any<FeedPost>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateFeedPostHandler_ModeratesAndUploadsImage_WhenImageBytesProvided()
    {
        var postRepo = Substitute.For<IFeedPostRepository>();
        var userRepo = Substitute.For<IUserRepository>();
        var currentUser = Substitute.For<ICurrentUserService>();
        var clock = Substitute.For<IDateTimeProvider>();
        var textModeration = Substitute.For<ITextModerationService>();
        var imageModeration = Substitute.For<IImageModerationService>();
        var mediaStorage = Substitute.For<IMediaStorage>();
        var auditRecorder = Substitute.For<IModerationAuditRecorder>();
        auditRecorder.RecordAsync(Arg.Any<ModerationAuditEntry>(), Arg.Any<CancellationToken>()).Returns(Result.Success());

        Guid userId = Guid.NewGuid();
        currentUser.UserId.Returns(userId.ToString());
        clock.UtcNow.Returns(Now);
        textModeration.ModerateContentTextAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new TextModerationResult(true));
        imageModeration.ModerateImageBytesAsync(Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(new ImageModerationResult(true, "Imagen aprobada."));
        mediaStorage.UploadImageAsync(Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new MediaUploadResult("posts/public-id", "https://res.cloudinary.com/sensus/image/upload/v1/posts/public-id.jpg")));

        var handler = new CreateFeedPostCommandHandler(
            postRepo,
            userRepo,
            currentUser,
            clock,
            NullLogger<CreateFeedPostCommandHandler>.Instance,
            textModerationService: textModeration,
            imageModerationService: imageModeration,
            mediaStorage: mediaStorage,
            auditRecorder: auditRecorder);

        Result<FeedPostDto> result = await handler.Handle(
            new CreateFeedPostCommand("Publicación con imagen", null, [1, 2, 3], "image/jpeg"),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.ImageUrl.Should().Be("https://res.cloudinary.com/sensus/image/upload/v1/posts/public-id.jpg");
        await imageModeration.Received(1).ModerateImageBytesAsync(
            Arg.Any<byte[]>(),
            "image/jpeg",
            "Publicación con imagen",
            Arg.Any<CancellationToken>());
        await mediaStorage.Received(1).UploadImageAsync(
            Arg.Any<byte[]>(),
            "image/jpeg",
            userId,
            Arg.Any<CancellationToken>());
        await postRepo.Received(1).AddAsync(
            Arg.Is<FeedPost>(p => p.ImagePublicId == "posts/public-id"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteFeedPostHandler_PurgesImage_WhenPostHasPublicId()
    {
        var postRepo = Substitute.For<IFeedPostRepository>();
        var currentUser = Substitute.For<ICurrentUserService>();
        var clock = Substitute.For<IDateTimeProvider>();
        var mediaStorage = Substitute.For<IMediaStorage>();

        Guid authorId = Guid.NewGuid();
        Guid postId = Guid.NewGuid();
        currentUser.UserId.Returns(authorId.ToString());
        currentUser.Roles.Returns(new List<string> { "Consumidor" });
        clock.UtcNow.Returns(Now);
        mediaStorage.DeleteImageAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);

        FeedPost post = FeedPost.Create(
            authorId,
            "Autor",
            "Post con imagen",
            "https://res.cloudinary.com/sensus/image/upload/v1/posts/public-id.jpg",
            Now,
            id: postId,
            imagePublicId: "posts/public-id").Value;
        postRepo.GetByIdAsync(postId, Arg.Any<CancellationToken>()).Returns(post);

        var handler = new DeleteFeedPostCommandHandler(
            postRepo,
            currentUser,
            clock,
            NullLogger<DeleteFeedPostCommandHandler>.Instance,
            mediaStorage);

        Result result = await handler.Handle(new DeleteFeedPostCommand(postId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await mediaStorage.Received(1).DeleteImageAsync("posts/public-id", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateFeedPost_RejectsExternalImageUrl()
    {
        var validator = new CreateFeedPostCommandValidator();
        var command = new CreateFeedPostCommand("Publicación con URL externa", ImageUrl: "https://evil.example.com/img.jpg");

        var validation = await validator.ValidateAsync(command, CancellationToken.None);
        validation.IsValid.Should().BeFalse();

        var postRepo = Substitute.For<IFeedPostRepository>();
        var userRepo = Substitute.For<IUserRepository>();
        var currentUser = Substitute.For<ICurrentUserService>();
        var clock = Substitute.For<IDateTimeProvider>();
        var textModeration = Substitute.For<ITextModerationService>();
        currentUser.UserId.Returns(Guid.NewGuid().ToString());
        clock.UtcNow.Returns(Now);
        textModeration.ModerateContentTextAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new TextModerationResult(true));

        var handler = new CreateFeedPostCommandHandler(
            postRepo,
            userRepo,
            currentUser,
            clock,
            NullLogger<CreateFeedPostCommandHandler>.Instance,
            textModerationService: textModeration);

        Result<FeedPostDto> result = await handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("FeedPost.ExternalImageUrlNotAllowed");
        await postRepo.DidNotReceive().AddAsync(Arg.Any<FeedPost>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateFeedPostHandler_AcceptsApprovedOwnedMediaAsset()
    {
        Guid userId = Guid.NewGuid();
        var asset = MediaAsset.Create(
            userId, "posts/approved-id", "https://res.cloudinary.com/sensus/image/upload/v1/approved.jpg",
            "image/jpeg", MediaAssetStatus.Approved, Now);

        var postRepo = Substitute.For<IFeedPostRepository>();
        var userRepo = Substitute.For<IUserRepository>();
        var currentUser = Substitute.For<ICurrentUserService>();
        var clock = Substitute.For<IDateTimeProvider>();
        var textModeration = Substitute.For<ITextModerationService>();
        var mediaAssetRepo = Substitute.For<IMediaAssetRepository>();
        var auditRecorder = Substitute.For<IModerationAuditRecorder>();
        auditRecorder.RecordAsync(Arg.Any<ModerationAuditEntry>(), Arg.Any<CancellationToken>()).Returns(Result.Success());
        currentUser.UserId.Returns(userId.ToString());
        clock.UtcNow.Returns(Now);
        textModeration.ModerateContentTextAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new TextModerationResult(true));
        mediaAssetRepo.GetByIdAsync(asset.Id, Arg.Any<CancellationToken>()).Returns(asset);

        var handler = new CreateFeedPostCommandHandler(
            postRepo, userRepo, currentUser, clock,
            NullLogger<CreateFeedPostCommandHandler>.Instance,
            textModerationService: textModeration,
            mediaAssetRepository: mediaAssetRepo,
            auditRecorder: auditRecorder);

        Result<FeedPostDto> result = await handler.Handle(
            new CreateFeedPostCommand("Publicación con asset aprobado", MediaAssetId: asset.Id),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.ImageUrl.Should().Be(asset.SecureUrl);
        await postRepo.Received(1).AddAsync(
            Arg.Is<FeedPost>(p => p.ImagePublicId == "posts/approved-id" && p.ImageUrl == asset.SecureUrl),
            Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(MediaAssetStatus.Quarantined, true)]
    [InlineData(MediaAssetStatus.Rejected, true)]
    [InlineData(MediaAssetStatus.Approved, false)]
    public async Task CreateFeedPostHandler_RejectsUnapprovedOrForeignMediaAsset(
        MediaAssetStatus status, bool sameOwner)
    {
        Guid userId = Guid.NewGuid();
        Guid ownerId = sameOwner ? userId : Guid.NewGuid();
        var asset = MediaAsset.Create(
            ownerId, "posts/other-id", "https://res.cloudinary.com/sensus/image/upload/v1/other.jpg",
            "image/jpeg", status, Now);

        var postRepo = Substitute.For<IFeedPostRepository>();
        var userRepo = Substitute.For<IUserRepository>();
        var currentUser = Substitute.For<ICurrentUserService>();
        var clock = Substitute.For<IDateTimeProvider>();
        var textModeration = Substitute.For<ITextModerationService>();
        var mediaAssetRepo = Substitute.For<IMediaAssetRepository>();
        var auditRecorder = Substitute.For<IModerationAuditRecorder>();
        auditRecorder.RecordAsync(Arg.Any<ModerationAuditEntry>(), Arg.Any<CancellationToken>()).Returns(Result.Success());
        currentUser.UserId.Returns(userId.ToString());
        clock.UtcNow.Returns(Now);
        textModeration.ModerateContentTextAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new TextModerationResult(true));
        mediaAssetRepo.GetByIdAsync(asset.Id, Arg.Any<CancellationToken>()).Returns(asset);

        var handler = new CreateFeedPostCommandHandler(
            postRepo, userRepo, currentUser, clock,
            NullLogger<CreateFeedPostCommandHandler>.Instance,
            textModerationService: textModeration,
            mediaAssetRepository: mediaAssetRepo,
            auditRecorder: auditRecorder);

        Result<FeedPostDto> result = await handler.Handle(
            new CreateFeedPostCommand("Publicación inválida", MediaAssetId: asset.Id),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("FeedPost.MediaNotApproved");
        await postRepo.DidNotReceive().AddAsync(Arg.Any<FeedPost>(), Arg.Any<CancellationToken>());
    }
}
