using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Media.Commands.PurgeQuarantinedMedia;
using Sensus.Domain.Entities;
using Sensus.Domain.Repositories;
using Xunit;

namespace Sensus.Application.UnitTests.Media.Commands.PurgeQuarantinedMedia;

public sealed class PurgeQuarantinedMediaCommandHandlerTests
{
    private static readonly DateTime Now = new(2026, 11, 13, 12, 0, 0, DateTimeKind.Utc);

    private readonly IMediaAssetRepository _repository = Substitute.For<IMediaAssetRepository>();
    private readonly IMediaStorage _storage = Substitute.For<IMediaStorage>();
    private readonly IDateTimeProvider _clock = Substitute.For<IDateTimeProvider>();
    private readonly PurgeQuarantinedMediaCommandHandler _handler;

    public PurgeQuarantinedMediaCommandHandlerTests()
    {
        _clock.UtcNow.Returns(Now);
        _handler = new PurgeQuarantinedMediaCommandHandler(
            _repository,
            _storage,
            _clock,
            Substitute.For<ILogger<PurgeQuarantinedMediaCommandHandler>>());
    }

    private static MediaAsset Quarantined(int ageDays) =>
        MediaAsset.Create(Guid.NewGuid(), $"pid-{Guid.NewGuid()}", "https://example.com/a.jpg", "image/jpeg",
            MediaAssetStatus.Quarantined, Now.AddDays(-ageDays));

    [Fact]
    public async Task Handle_WhenRetentionInvalid_ShouldReturnValidationError()
    {
        var result = await _handler.Handle(new PurgeQuarantinedMediaCommand(0), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Media.RetentionInvalid");
    }

    [Fact]
    public async Task Handle_WhenAssetsExpired_ShouldDeleteFromStorageAndPurge()
    {
        MediaAsset expired = Quarantined(31);
        _repository.GetQuarantinedCreatedBeforeAsync(Arg.Any<DateTime>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new List<MediaAsset> { expired });
        _storage.DeleteImageAsync(expired.PublicId, Arg.Any<CancellationToken>()).Returns(true);

        var result = await _handler.Handle(new PurgeQuarantinedMediaCommand(30), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.PurgedCount.Should().Be(1);
        result.Value.FailedCount.Should().Be(0);
        expired.Status.Should().Be(MediaAssetStatus.Rejected);
        expired.SecureUrl.Should().BeNull();
        await _repository.Received(1).UpdateAsync(expired, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenStorageDeletionFails_ShouldKeepAssetForRetry()
    {
        MediaAsset expired = Quarantined(40);
        _repository.GetQuarantinedCreatedBeforeAsync(Arg.Any<DateTime>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new List<MediaAsset> { expired });
        _storage.DeleteImageAsync(expired.PublicId, Arg.Any<CancellationToken>()).Returns(false);

        var result = await _handler.Handle(new PurgeQuarantinedMediaCommand(30), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.PurgedCount.Should().Be(0);
        result.Value.FailedCount.Should().Be(1);
        expired.Status.Should().Be(MediaAssetStatus.Quarantined);
        await _repository.DidNotReceive().UpdateAsync(Arg.Any<MediaAsset>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenNothingExpired_ShouldReturnZeroCounts()
    {
        _repository.GetQuarantinedCreatedBeforeAsync(Arg.Any<DateTime>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new List<MediaAsset>());

        var result = await _handler.Handle(new PurgeQuarantinedMediaCommand(30), CancellationToken.None);

        result.Value.PurgedCount.Should().Be(0);
        await _storage.DidNotReceive().DeleteImageAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}
