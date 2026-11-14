using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Media.Commands.ReleaseQuarantinedMedia;
using Sensus.Domain.Common;
using Sensus.Domain.Entities;
using Sensus.Domain.Repositories;
using Xunit;

namespace Sensus.Application.UnitTests.Media.Commands.ReleaseQuarantinedMedia;

/// <summary>
/// Pruebas de la liberación manual de contenido en cuarentena (Spec 019, FR-019-04, CA-019-03).
/// </summary>
public sealed class ReleaseQuarantinedMediaCommandHandlerTests
{
    private static readonly DateTime Now = new(2026, 11, 13, 12, 0, 0, DateTimeKind.Utc);

    private readonly IMediaAssetRepository _repository = Substitute.For<IMediaAssetRepository>();
    private readonly ICurrentUserService _currentUser = Substitute.For<ICurrentUserService>();
    private readonly IDateTimeProvider _clock = Substitute.For<IDateTimeProvider>();
    private readonly ReleaseQuarantinedMediaCommandHandler _handler;

    public ReleaseQuarantinedMediaCommandHandlerTests()
    {
        _clock.UtcNow.Returns(Now);
        _currentUser.UserId.Returns(Guid.NewGuid().ToString());
        _handler = new ReleaseQuarantinedMediaCommandHandler(
            _repository,
            _currentUser,
            _clock,
            Substitute.For<ILogger<ReleaseQuarantinedMediaCommandHandler>>());
    }

    private static MediaAsset Asset(MediaAssetStatus status) =>
        MediaAsset.Create(Guid.NewGuid(), "pid-1", "https://example.com/a.jpg", "image/jpeg", status, Now.AddDays(-1));

    [Fact]
    public async Task Handle_WhenAssetIsQuarantined_ShouldApproveAndExposeUrl()
    {
        MediaAsset asset = Asset(MediaAssetStatus.Quarantined);
        _repository.GetByIdAsync(asset.Id, Arg.Any<CancellationToken>()).Returns(asset);

        var result = await _handler.Handle(new ReleaseQuarantinedMediaCommand(asset.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.MediaId.Should().Be(asset.Id);
        result.Value.Status.Should().Be(nameof(MediaAssetStatus.Approved));
        result.Value.SecureUrl.Should().Be("https://example.com/a.jpg");
        result.Value.ReleasedAtUtc.Should().Be(Now);
        await _repository.Received(1).UpdateAsync(
            Arg.Is<MediaAsset>(a => a.Id == asset.Id && a.Status == MediaAssetStatus.Approved),
            Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(MediaAssetStatus.Approved)]
    [InlineData(MediaAssetStatus.Rejected)]
    [InlineData(MediaAssetStatus.Pending)]
    public async Task Handle_WhenAssetIsNotQuarantined_ShouldReturnConflictWithoutUpdating(MediaAssetStatus status)
    {
        MediaAsset asset = Asset(status);
        _repository.GetByIdAsync(asset.Id, Arg.Any<CancellationToken>()).Returns(asset);

        var result = await _handler.Handle(new ReleaseQuarantinedMediaCommand(asset.Id), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Conflict);
        result.Error.Code.Should().Be("Media.NotQuarantined");
        asset.Status.Should().Be(status);
        await _repository.DidNotReceiveWithAnyArgs().UpdateAsync(default!, default);
    }

    [Fact]
    public async Task Handle_WhenAssetDoesNotExist_ShouldReturnNotFound()
    {
        _repository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((MediaAsset?)null);

        var result = await _handler.Handle(new ReleaseQuarantinedMediaCommand(Guid.NewGuid()), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
        await _repository.DidNotReceiveWithAnyArgs().UpdateAsync(default!, default);
    }

    [Fact]
    public async Task Handle_WhenIdIsEmpty_ShouldReturnValidationErrorWithoutQuerying()
    {
        var result = await _handler.Handle(new ReleaseQuarantinedMediaCommand(Guid.Empty), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        await _repository.DidNotReceiveWithAnyArgs().GetByIdAsync(default, default);
    }
}
