// Ref: FR-023-01 / FR-023-02 / B1 - Emisión de URLs de entrega fail-closed (solo Approved)
using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Common.Settings;
using Sensus.Application.Media.Delivery;
using Sensus.Domain.Common;
using Sensus.Domain.Entities;
using Sensus.Domain.Enums;
using Sensus.Domain.Repositories;
using Xunit;

namespace Sensus.Application.UnitTests.Media.Delivery;

public sealed class CreateMediaDeliveryUrlCommandHandlerTests
{
    private static readonly DateTime Now = new(2026, 11, 22, 10, 0, 0, DateTimeKind.Utc);

    private readonly IMediaAssetRepository _mediaAssets = Substitute.For<IMediaAssetRepository>();
    private readonly IMediaDeliveryUrlGenerator _generator = Substitute.For<IMediaDeliveryUrlGenerator>();
    private readonly IOptions<MediaDeliverySettings> _settings = Substitute.For<IOptions<MediaDeliverySettings>>();
    private readonly CreateMediaDeliveryUrlCommandHandler _handler;

    private readonly Guid _ownerId = Guid.NewGuid();

    public CreateMediaDeliveryUrlCommandHandlerTests()
    {
        _settings.Value.Returns(new MediaDeliverySettings
        {
            PublicTtlMinutes = 60,
            PublicCacheSeconds = 3600
        });

        _handler = new CreateMediaDeliveryUrlCommandHandler(
            _mediaAssets,
            _generator,
            _settings);
    }

    private MediaAsset CreateAsset(MediaAssetStatus status) =>
        MediaAsset.Create(
            _ownerId,
            "sample_public_id",
            "https://res.cloudinary.com/demo/image/upload/sample.jpg",
            "image/jpeg",
            status,
            Now);

    [Fact]
    public async Task Handle_WhenAssetIsApproved_GeneratesDeliveryUrl()
    {
        MediaAsset asset = CreateAsset(MediaAssetStatus.Approved);
        _mediaAssets.GetByIdAsync(asset.Id, Arg.Any<CancellationToken>()).Returns(asset);

        DateTime expiresAt = Now.AddMinutes(60);
        string expectedUrl = $"https://localhost:5001/api/v1/media/{asset.Id}/delivery?expires=123&signature=abc";
        _generator.Generate(asset, TimeSpan.FromMinutes(60))
            .Returns(Result.Success(new MediaDeliveryUrl(expectedUrl, expiresAt, MediaDeliveryScope.Public)));

        var command = new CreateMediaDeliveryUrlCommand(asset.Id);
        Result<MediaDeliveryUrlDto> result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        MediaDeliveryUrlDto dto = result.Value;
        dto.MediaId.Should().Be(asset.Id);
        dto.DeliveryUrl.Should().Be(expectedUrl);
        dto.ExpiresAtUtc.Should().Be(expiresAt);
        dto.Scope.Should().Be("Public");
        dto.Status.Should().Be("Approved");
        dto.CacheMaxAgeSeconds.Should().Be(3600);
    }

    [Theory]
    [InlineData(MediaAssetStatus.Quarantined)]
    [InlineData(MediaAssetStatus.Pending)]
    [InlineData(MediaAssetStatus.Rejected)]
    public async Task Handle_WhenAssetIsNotApproved_ReturnsNotFound_FailClosed(MediaAssetStatus status)
    {
        // Ref: B1 / Decision 2 - contenido en cuarentena o no aprobado nunca se entrega por este endpoint
        MediaAsset asset = CreateAsset(status);
        _mediaAssets.GetByIdAsync(asset.Id, Arg.Any<CancellationToken>()).Returns(asset);

        var command = new CreateMediaDeliveryUrlCommand(asset.Id);
        Result<MediaDeliveryUrlDto> result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Media.NotFound");
        result.Error.Type.Should().Be(ErrorType.NotFound);
        _generator.DidNotReceive().Generate(Arg.Any<MediaAsset>(), Arg.Any<TimeSpan>());
    }

    [Fact]
    public async Task Handle_WhenAssetDoesNotExist_ReturnsNotFound()
    {
        _mediaAssets.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((MediaAsset?)null);

        var command = new CreateMediaDeliveryUrlCommand(Guid.NewGuid());
        Result<MediaDeliveryUrlDto> result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Media.NotFound");
        result.Error.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task Handle_WhenGeneratorFails_ReturnsPropagatedError()
    {
        MediaAsset asset = CreateAsset(MediaAssetStatus.Approved);
        _mediaAssets.GetByIdAsync(asset.Id, Arg.Any<CancellationToken>()).Returns(asset);

        _generator.Generate(asset, Arg.Any<TimeSpan>())
            .Returns(Result.Failure<MediaDeliveryUrl>(Error.Unavailable("MediaDelivery.NotConfigured", "Clave no configurada.")));

        var command = new CreateMediaDeliveryUrlCommand(asset.Id);
        Result<MediaDeliveryUrlDto> result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("MediaDelivery.NotConfigured");
    }
}
