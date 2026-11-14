using FluentAssertions;
using NSubstitute;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Media.Commands.UploadMedia;
using Sensus.Application.Media.DTOs;
using Sensus.Domain.Common;
using Sensus.Domain.Entities;
using Sensus.Domain.Repositories;
using Xunit;

namespace Sensus.Application.UnitTests.Media.Commands.UploadMedia;

public sealed class UploadMediaCommandHandlerTests
{
    private readonly IMediaStorage _mediaStorage = Substitute.For<IMediaStorage>();
    private readonly IImageModerationService _moderationService = Substitute.For<IImageModerationService>();
    private readonly IMediaAssetRepository _mediaAssetRepository = Substitute.For<IMediaAssetRepository>();
    private readonly ICurrentUserService _currentUser = Substitute.For<ICurrentUserService>();
    private readonly IDateTimeProvider _dateTimeProvider = Substitute.For<IDateTimeProvider>();

    private readonly Guid _userId = Guid.NewGuid();
    private readonly UploadMediaCommandHandler _handler;

    public UploadMediaCommandHandlerTests()
    {
        _handler = new UploadMediaCommandHandler(
            _mediaStorage,
            _moderationService,
            _mediaAssetRepository,
            _currentUser,
            _dateTimeProvider);
    }

    [Fact]
    public async Task Handle_WhenUserNotAuthenticated_ShouldReturnUnauthorized()
    {
        // Arrange
        _currentUser.UserId.Returns((string?)null);
        var command = new UploadMediaCommand(new byte[] { 0xFF, 0xD8, 0xFF }, "image/jpeg");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Media.Unauthenticated");
    }

    [Fact]
    public async Task Handle_WhenDailyLimitExceeded_ShouldReturnValidationError()
    {
        // Arrange
        _currentUser.UserId.Returns(_userId.ToString());
        _dateTimeProvider.UtcNow.Returns(new DateTime(2026, 11, 13, 10, 0, 0, DateTimeKind.Utc));
        _mediaAssetRepository.CountByUserAndDateAsync(_userId, Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(20);
        var command = new UploadMediaCommand(new byte[] { 0xFF, 0xD8, 0xFF }, "image/jpeg");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Media.DailyLimitExceeded");
    }

    [Fact]
    public async Task Handle_WhenUploadFails_ShouldReturnFailure()
    {
        // Arrange
        _currentUser.UserId.Returns(_userId.ToString());
        _dateTimeProvider.UtcNow.Returns(new DateTime(2026, 11, 13, 10, 0, 0, DateTimeKind.Utc));
        _mediaAssetRepository.CountByUserAndDateAsync(_userId, Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(0);
        _mediaStorage.UploadImageAsync(Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<MediaUploadResult>(Error.Failure("Media.UploadFailed", "Error de subida")));
        var command = new UploadMediaCommand(new byte[] { 0xFF, 0xD8, 0xFF }, "image/jpeg");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Media.UploadFailed");
    }

    [Fact]
    public async Task Handle_WhenModerationApproved_ShouldReturnSuccessWithApprovedStatus()
    {
        // Arrange
        _currentUser.UserId.Returns(_userId.ToString());
        _dateTimeProvider.UtcNow.Returns(new DateTime(2026, 11, 13, 10, 0, 0, DateTimeKind.Utc));
        _mediaAssetRepository.CountByUserAndDateAsync(_userId, Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(0);
        _mediaStorage.UploadImageAsync(Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new MediaUploadResult("public-id-123", "https://cloudinary.com/img.jpg")));
        _moderationService.ModerateImageBytesAsync(Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ImageModerationResult(true, "Imagen aprobada.", [], "Approved"));
        var command = new UploadMediaCommand(new byte[] { 0xFF, 0xD8, 0xFF }, "image/jpeg");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be("Approved");
        result.Value.MediaId.Should().NotBe(Guid.Empty);
        result.Value.PublicId.Should().Be("public-id-123");
        result.Value.SecureUrl.Should().Be("https://cloudinary.com/img.jpg");
        await _mediaStorage.DidNotReceive().DeleteImageAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _mediaAssetRepository.Received(1).AddAsync(Arg.Any<MediaAsset>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenModerationQuarantined_ShouldReturnSuccessWithQuarantinedStatus()
    {
        // Arrange
        _currentUser.UserId.Returns(_userId.ToString());
        _dateTimeProvider.UtcNow.Returns(new DateTime(2026, 11, 13, 10, 0, 0, DateTimeKind.Utc));
        _mediaAssetRepository.CountByUserAndDateAsync(_userId, Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(0);
        _mediaStorage.UploadImageAsync(Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new MediaUploadResult("public-id-123", "https://cloudinary.com/img.jpg")));
        _moderationService.ModerateImageBytesAsync(Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ImageModerationResult(false, "En cuarentena.", ["suspicious"], "Quarantine"));
        var command = new UploadMediaCommand(new byte[] { 0xFF, 0xD8, 0xFF }, "image/jpeg");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be("Quarantined");
        result.Value.ModerationDecision.Should().Be("Quarantine");
        result.Value.SecureUrl.Should().BeNull();
        await _mediaStorage.DidNotReceive().DeleteImageAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _mediaAssetRepository.Received(1).AddAsync(
            Arg.Is<MediaAsset>(a => a.Status == MediaAssetStatus.Quarantined && a.SecureUrl != null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenModerationRejected_ShouldReturnSuccessWithRejectedStatus()
    {
        // Arrange
        _currentUser.UserId.Returns(_userId.ToString());
        _dateTimeProvider.UtcNow.Returns(new DateTime(2026, 11, 13, 10, 0, 0, DateTimeKind.Utc));
        _mediaAssetRepository.CountByUserAndDateAsync(_userId, Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(0);
        _mediaStorage.UploadImageAsync(Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new MediaUploadResult("public-id-123", "https://cloudinary.com/img.jpg")));
        _moderationService.ModerateImageBytesAsync(Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ImageModerationResult(false, "Rechazado por políticas.", ["weapons"], "Rejected"));
        var command = new UploadMediaCommand(new byte[] { 0xFF, 0xD8, 0xFF }, "image/jpeg");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be("Rejected");
        result.Value.ModerationDecision.Should().Be("Rejected");
        result.Value.SecureUrl.Should().BeNull();
        await _mediaStorage.Received(1).DeleteImageAsync("public-id-123", Arg.Any<CancellationToken>());
        await _mediaAssetRepository.Received(1).AddAsync(
            Arg.Is<MediaAsset>(a => a.Status == MediaAssetStatus.Rejected && a.SecureUrl == null),
            Arg.Any<CancellationToken>());
    }
}
