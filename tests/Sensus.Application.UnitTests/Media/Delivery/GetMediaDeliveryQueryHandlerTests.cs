// Ref: FR-023-01 / FR-023-02 / B1 / B2 - Entrega de activos aprobados mediante proxy seguro y cabeceras acotadas
using FluentAssertions;
using NSubstitute;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Media.Delivery;
using Sensus.Domain.Common;
using Sensus.Domain.Entities;
using Sensus.Domain.Enums;
using Sensus.Domain.Repositories;
using Xunit;

namespace Sensus.Application.UnitTests.Media.Delivery;

public sealed class GetMediaDeliveryQueryHandlerTests
{
    private static readonly DateTime Now = new(2026, 11, 22, 10, 0, 0, DateTimeKind.Utc);
    private static readonly long NowUnix = new DateTimeOffset(Now).ToUnixTimeSeconds();

    private readonly IMediaAssetRepository _mediaAssets = Substitute.For<IMediaAssetRepository>();
    private readonly IMediaDeliveryUrlGenerator _generator = Substitute.For<IMediaDeliveryUrlGenerator>();
    private readonly IMediaContentProxy _contentProxy = Substitute.For<IMediaContentProxy>();
    private readonly IDateTimeProvider _clock = Substitute.For<IDateTimeProvider>();
    private readonly GetMediaDeliveryQueryHandler _handler;

    public GetMediaDeliveryQueryHandlerTests()
    {
        _clock.UtcNow.Returns(Now);

        _handler = new GetMediaDeliveryQueryHandler(
            _mediaAssets,
            _generator,
            _contentProxy,
            _clock);
    }

    private static MediaAsset CreateAsset(
        MediaAssetStatus status,
        string secureUrl = "https://res.cloudinary.com/demo/image/upload/sample.jpg",
        string contentType = "image/jpeg") =>
        MediaAsset.Create(Guid.NewGuid(), "sample_pid", secureUrl, contentType, status, Now);

    [Fact]
    public async Task Handle_WhenApprovedAssetWithValidSignature_StreamsContentWithRemainingTtlCacheControl()
    {
        MediaAsset asset = CreateAsset(MediaAssetStatus.Approved);
        _mediaAssets.GetByIdAsync(asset.Id, Arg.Any<CancellationToken>()).Returns(asset);

        long expires = NowUnix + 1800; // 30 minutes remaining
        string signature = "valid_signature_123";

        _generator.Validate(asset.Id, expires, signature, MediaDeliveryScope.Public).Returns(true);

        using var memoryStream = new MemoryStream([1, 2, 3, 4]);
        _contentProxy.OpenReadAsync(asset, Arg.Any<CancellationToken>())
            .Returns(Result.Success(new MediaContentPayload(memoryStream, "image/jpeg")));

        var query = new GetMediaDeliveryQuery(asset.Id, expires, signature);
        Result<MediaDeliveryResponse> result = await _handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        MediaDeliveryResponse response = result.Value;
        response.IsRedirect.Should().BeFalse();
        response.Content.Should().BeSameAs(memoryStream);
        response.ContentType.Should().Be("image/jpeg");
        response.CacheControl.Should().Be("public, max-age=1800, s-maxage=1800");
    }

    [Theory]
    [InlineData(MediaAssetStatus.Quarantined)]
    [InlineData(MediaAssetStatus.Pending)]
    [InlineData(MediaAssetStatus.Rejected)]
    public async Task Handle_WhenAssetIsNotApproved_ReturnsNotFound_FailClosed(MediaAssetStatus status)
    {
        // Ref: B1 / B2 / CA-023-03 - cualquier activo no aprobado o retirado responde 404
        MediaAsset asset = CreateAsset(status);
        _mediaAssets.GetByIdAsync(asset.Id, Arg.Any<CancellationToken>()).Returns(asset);

        long expires = NowUnix + 600;
        var query = new GetMediaDeliveryQuery(asset.Id, expires, "sig");
        Result<MediaDeliveryResponse> result = await _handler.Handle(query, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Media.NotFound");
        result.Error.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task Handle_WhenAssetDoesNotExist_ReturnsNotFound()
    {
        long expires = NowUnix + 600;
        _mediaAssets.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((MediaAsset?)null);

        var query = new GetMediaDeliveryQuery(Guid.NewGuid(), expires, "some_sig");
        Result<MediaDeliveryResponse> result = await _handler.Handle(query, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Media.NotFound");
        result.Error.Type.Should().Be(ErrorType.NotFound);
    }

    [Theory]
    [InlineData(0, "valid_sig")]
    [InlineData(-1, "valid_sig")]
    [InlineData(1000, "")]
    [InlineData(1000, "   ")]
    [InlineData(1000, null)]
    public async Task Handle_WhenExpiresOrSignatureMissing_ReturnsForbidden(long expires, string? signature)
    {
        var query = new GetMediaDeliveryQuery(Guid.NewGuid(), expires, signature);
        Result<MediaDeliveryResponse> result = await _handler.Handle(query, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Media.SignatureRequired");
        result.Error.Type.Should().Be(ErrorType.Forbidden);
    }

    [Fact]
    public async Task Handle_WhenExpiresIsPast_ReturnsForbiddenExpired()
    {
        long pastExpires = NowUnix - 10;
        var query = new GetMediaDeliveryQuery(Guid.NewGuid(), pastExpires, "sig_past");

        Result<MediaDeliveryResponse> result = await _handler.Handle(query, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Media.SignatureExpired");
        result.Error.Type.Should().Be(ErrorType.Forbidden);
    }

    [Fact]
    public async Task Handle_WhenSignatureIsInvalid_ReturnsForbidden()
    {
        MediaAsset asset = CreateAsset(MediaAssetStatus.Approved);
        _mediaAssets.GetByIdAsync(asset.Id, Arg.Any<CancellationToken>()).Returns(asset);

        long expires = NowUnix + 600;
        _generator.Validate(asset.Id, expires, "tampered_sig", MediaDeliveryScope.Public).Returns(false);

        var query = new GetMediaDeliveryQuery(asset.Id, expires, "tampered_sig");
        Result<MediaDeliveryResponse> result = await _handler.Handle(query, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Media.InvalidSignature");
        result.Error.Type.Should().Be(ErrorType.Forbidden);
    }

    [Fact]
    public async Task Handle_WhenAssetHasEmptySecureUrl_ReturnsUnavailable()
    {
        MediaAsset asset = MediaAsset.Create(Guid.NewGuid(), "pid", "", "image/jpeg", MediaAssetStatus.Approved, Now);
        _mediaAssets.GetByIdAsync(asset.Id, Arg.Any<CancellationToken>()).Returns(asset);

        long expires = NowUnix + 600;
        _generator.Validate(asset.Id, expires, "sig", MediaDeliveryScope.Public).Returns(true);

        var query = new GetMediaDeliveryQuery(asset.Id, expires, "sig");
        Result<MediaDeliveryResponse> result = await _handler.Handle(query, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Media.ContentUnavailable");
    }

    [Fact]
    public async Task Handle_WhenContentProxyFails_ReturnsUnavailableError()
    {
        MediaAsset asset = CreateAsset(MediaAssetStatus.Approved);
        _mediaAssets.GetByIdAsync(asset.Id, Arg.Any<CancellationToken>()).Returns(asset);

        long expires = NowUnix + 300;
        _generator.Validate(asset.Id, expires, "sig", MediaDeliveryScope.Public).Returns(true);

        _contentProxy.OpenReadAsync(asset, Arg.Any<CancellationToken>())
            .Returns(Result.Failure<MediaContentPayload>(Error.Unavailable("Proxy.Failed", "Error del origen.")));

        var query = new GetMediaDeliveryQuery(asset.Id, expires, "sig");
        Result<MediaDeliveryResponse> result = await _handler.Handle(query, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Proxy.Failed");
    }
}
