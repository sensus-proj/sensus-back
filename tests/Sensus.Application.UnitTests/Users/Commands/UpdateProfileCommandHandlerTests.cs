using FluentAssertions;
using NSubstitute;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Users.Commands.UpdateProfile;
using Sensus.Domain.Entities;
using Sensus.Domain.Repositories;

namespace Sensus.Application.UnitTests.Users.Commands;

public class UpdateProfileCommandHandlerTests
{
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly IModerationAuditRecorder _auditRecorder = Substitute.For<IModerationAuditRecorder>();
    private readonly ICdnCacheInvalidator _cdnCacheInvalidator = Substitute.For<ICdnCacheInvalidator>();
    private readonly ITextModerationService _textModerationService = Substitute.For<ITextModerationService>();
    private readonly IMediaAssetRepository _mediaAssetRepository = Substitute.For<IMediaAssetRepository>();
    private readonly UpdateProfileCommandHandler _handler;

    public UpdateProfileCommandHandlerTests()
    {
        _auditRecorder.RecordAsync(Arg.Any<ModerationAuditEntry>(), Arg.Any<CancellationToken>())
            .Returns(Sensus.Domain.Common.Result.Success());
        _textModerationService.ModerateContentTextAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new TextModerationResult(true, "Aprobado", null, "Approved"));

        _handler = new UpdateProfileCommandHandler(
            _userRepository,
            _currentUserService,
            _auditRecorder,
            _cdnCacheInvalidator,
            textModerationService: _textModerationService,
            mediaAssetRepository: _mediaAssetRepository);
    }

    private User CreateExistingUser(Guid userId)
    {
        var existingUser = User.Create(
            "Old Name",
            "user@sensus.com",
            "hashed-pw",
            true,
            DateTime.UtcNow).Value;
        typeof(User).GetProperty("Id")?.SetValue(existingUser, userId);
        _userRepository.GetByIdAsync(userId, Arg.Any<CancellationToken>()).Returns(existingUser);
        return existingUser;
    }

    [Fact]
    public async Task Handle_ShouldUpdateProfileSuccessfully_WhenPhotoIsApprovedAndOwned()
    {
        var userId = Guid.NewGuid();
        _currentUserService.UserId.Returns(userId.ToString());
        var existingUser = CreateExistingUser(userId);
        const string photoUrl = "https://res.cloudinary.com/sensus/image/upload/avatar.jpg";
        _mediaAssetRepository.IsApprovedOwnedUrlAsync(userId, photoUrl, Arg.Any<CancellationToken>()).Returns(true);

        var result = await _handler.Handle(new UpdateProfileCommand("New Name", photoUrl), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.NombreCompleto.Should().Be("New Name");
        result.Value.FotoPerfilUrl.Should().Be(photoUrl);
        await _userRepository.Received(1).UpdateAsync(existingUser, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldClearPhoto_WhenFotoPerfilUrlIsNullOrEmpty()
    {
        var userId = Guid.NewGuid();
        _currentUserService.UserId.Returns(userId.ToString());
        var existingUser = CreateExistingUser(userId);
        existingUser.UpdateProfile("Old Name", "https://res.cloudinary.com/sensus/image/upload/old.jpg");

        var result = await _handler.Handle(new UpdateProfileCommand("New Name", null), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.FotoPerfilUrl.Should().BeNull();
        await _mediaAssetRepository.DidNotReceive().IsApprovedOwnedUrlAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldReturnUnauthorized_WhenCallerHasNoUserId()
    {
        _currentUserService.UserId.Returns((string?)null);

        var result = await _handler.Handle(new UpdateProfileCommand("New Name", null), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Auth.Unauthorized");
    }

    [Fact]
    public async Task Handle_ShouldReturnNotFound_WhenUserDoesNotExist()
    {
        var userId = Guid.NewGuid();
        _currentUserService.UserId.Returns(userId.ToString());
        _userRepository.GetByIdAsync(userId, Arg.Any<CancellationToken>()).Returns((User?)null);

        var result = await _handler.Handle(new UpdateProfileCommand("New Name", null), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("User.NotFound");
    }

    [Fact]
    public async Task Handle_ShouldReturnValidationError_WhenPhotoBelongsToAnotherUserOrQuarantined()
    {
        var userId = Guid.NewGuid();
        _currentUserService.UserId.Returns(userId.ToString());
        CreateExistingUser(userId);
        const string foreignOrQuarantinedUrl = "https://res.cloudinary.com/sensus/image/upload/foreign.jpg";
        _mediaAssetRepository.IsApprovedOwnedUrlAsync(userId, foreignOrQuarantinedUrl, Arg.Any<CancellationToken>()).Returns(false);

        var result = await _handler.Handle(new UpdateProfileCommand("Name", foreignOrQuarantinedUrl), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Profile.PhotoNotModerated");
        await _userRepository.DidNotReceive().UpdateAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
    }
}
