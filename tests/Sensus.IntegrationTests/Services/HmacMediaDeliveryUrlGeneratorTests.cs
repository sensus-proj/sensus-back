using System.Globalization;
using System.Security.Cryptography;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Common.Settings;
using Sensus.Domain.Common;
using Sensus.Domain.Entities;
using Sensus.Infrastructure.Services;
using Xunit;

namespace Sensus.IntegrationTests.Services;

/// <summary>
/// Pruebas para HmacMediaDeliveryUrlGenerator (Spec 023).
/// Ref: FR-023-02 - generación y validación de URLs firmadas con HMAC-SHA256 y TTL estricto.
/// </summary>
public sealed class HmacMediaDeliveryUrlGeneratorTests
{
    private static readonly DateTime Now = new(2026, 11, 22, 10, 0, 0, DateTimeKind.Utc);
    private readonly IDateTimeProvider _clock = Substitute.For<IDateTimeProvider>();
    private readonly ILogger<HmacMediaDeliveryUrlGenerator> _logger = Substitute.For<ILogger<HmacMediaDeliveryUrlGenerator>>();

    private readonly string _signingKeyBase64;
    private readonly MediaDeliverySettings _settings;

    public HmacMediaDeliveryUrlGeneratorTests()
    {
        _clock.UtcNow.Returns(Now);
        _signingKeyBase64 = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        _settings = new MediaDeliverySettings
        {
            SigningKey = _signingKeyBase64,
            PublicBaseUrl = "https://cdn.sensus.com",
            PublicTtlMinutes = 60,
            PrivateTtlMinutes = 5
        };
    }

    private HmacMediaDeliveryUrlGenerator CreateGenerator(MediaDeliverySettings? customSettings = null) =>
        new(Options.Create(customSettings ?? _settings), _clock, _logger);

    private static MediaAsset CreateAsset(
        MediaAssetStatus status,
        string secureUrl = "https://res.cloudinary.com/demo/image/upload/sample.jpg") =>
        MediaAsset.Create(Guid.NewGuid(), "pid-1", secureUrl, "image/jpeg", status, Now);

    [Fact]
    public void Generate_WhenAssetIsApproved_CreatesPublicUrlWithCorrectSignature()
    {
        // Ref: FR-023-01 / FR-023-02
        var generator = CreateGenerator();
        MediaAsset asset = CreateAsset(MediaAssetStatus.Approved);

        Result<MediaDeliveryUrl> result = generator.Generate(asset, TimeSpan.FromMinutes(60));

        result.IsSuccess.Should().BeTrue();
        MediaDeliveryUrl delivery = result.Value;
        delivery.Scope.Should().Be(MediaDeliveryScope.Public);
        delivery.ExpiresAtUtc.Should().Be(Now.AddMinutes(60));
        delivery.Url.Should().StartWith($"https://cdn.sensus.com/api/v1/media/{asset.Id}/delivery?expires=");
        delivery.Url.Should().Contain("&signature=");

        // Extraer parámetros para validar
        var uri = new Uri(delivery.Url);
        var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
        long expires = long.Parse(query["expires"]!, CultureInfo.InvariantCulture);
        string signature = query["signature"]!;

        bool isValid = generator.Validate(asset.Id, expires, signature, MediaDeliveryScope.Public);
        isValid.Should().BeTrue();
    }

    [Fact]
    public void Generate_WhenAssetIsQuarantined_CreatesPrivateUrlWithCorrectSignature()
    {
        // Ref: FR-023-02 - contenido privado con scope Private
        var generator = CreateGenerator();
        MediaAsset asset = CreateAsset(MediaAssetStatus.Quarantined);

        Result<MediaDeliveryUrl> result = generator.Generate(asset, TimeSpan.FromMinutes(5));

        result.IsSuccess.Should().BeTrue();
        MediaDeliveryUrl delivery = result.Value;
        delivery.Scope.Should().Be(MediaDeliveryScope.Private);
        delivery.ExpiresAtUtc.Should().Be(Now.AddMinutes(5));

        var uri = new Uri(delivery.Url);
        var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
        long expires = long.Parse(query["expires"]!, CultureInfo.InvariantCulture);
        string signature = query["signature"]!;

        // Válido con scope Private
        generator.Validate(asset.Id, expires, signature, MediaDeliveryScope.Private).Should().BeTrue();
        // Inválido si se intenta validar con scope Public
        generator.Validate(asset.Id, expires, signature, MediaDeliveryScope.Public).Should().BeFalse();
    }

    [Fact]
    public void Validate_WhenSignatureIsTampered_ReturnsFalse()
    {
        // Ref: FR-023-02 / CA-023-02 - rechazo ante firma alterada
        var generator = CreateGenerator();
        MediaAsset asset = CreateAsset(MediaAssetStatus.Approved);

        Result<MediaDeliveryUrl> result = generator.Generate(asset, TimeSpan.FromMinutes(30));
        var uri = new Uri(result.Value.Url);
        var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
        long expires = long.Parse(query["expires"]!, CultureInfo.InvariantCulture);

        generator.Validate(asset.Id, expires, "tampered_fake_signature", MediaDeliveryScope.Public).Should().BeFalse();
    }

    [Fact]
    public void Validate_WhenMediaIdIsAltered_ReturnsFalse()
    {
        // Ref: FR-023-02 - la firma está vinculada estrictamente al MediaId
        var generator = CreateGenerator();
        MediaAsset asset = CreateAsset(MediaAssetStatus.Approved);

        Result<MediaDeliveryUrl> result = generator.Generate(asset, TimeSpan.FromMinutes(30));
        var uri = new Uri(result.Value.Url);
        var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
        long expires = long.Parse(query["expires"]!, CultureInfo.InvariantCulture);
        string signature = query["signature"]!;

        generator.Validate(Guid.NewGuid(), expires, signature, MediaDeliveryScope.Public).Should().BeFalse();
    }

    [Fact]
    public void Validate_WhenExpirationIsAltered_ReturnsFalse()
    {
        // Ref: FR-023-02 - la firma está vinculada al timestamp de expiración
        var generator = CreateGenerator();
        MediaAsset asset = CreateAsset(MediaAssetStatus.Approved);

        Result<MediaDeliveryUrl> result = generator.Generate(asset, TimeSpan.FromMinutes(30));
        var uri = new Uri(result.Value.Url);
        var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
        long expires = long.Parse(query["expires"]!, CultureInfo.InvariantCulture);
        string signature = query["signature"]!;

        generator.Validate(asset.Id, expires + 10, signature, MediaDeliveryScope.Public).Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("short_key_under_32_bytes")]
    public void Generate_WhenSigningKeyIsTooShortOrEmpty_ReturnsUnavailableError(string? key)
    {
        // Ref: FR-023-02 - fail-closed si la clave de firma no cumple la longitud criptográfica mínima (32 bytes)
        var settings = new MediaDeliverySettings
        {
            SigningKey = key ?? string.Empty,
            PublicBaseUrl = "https://cdn.sensus.com"
        };
        var generator = CreateGenerator(settings);
        MediaAsset asset = CreateAsset(MediaAssetStatus.Approved);

        Result<MediaDeliveryUrl> result = generator.Generate(asset, TimeSpan.FromMinutes(30));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("MediaDelivery.NotConfigured");
    }

    [Fact]
    public void Generate_WhenAssetHasEmptySecureUrl_ReturnsUnavailableError()
    {
        var generator = CreateGenerator();
        MediaAsset asset = CreateAsset(MediaAssetStatus.Approved, secureUrl: "");

        Result<MediaDeliveryUrl> result = generator.Generate(asset, TimeSpan.FromMinutes(30));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("MediaDelivery.ContentUnavailable");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Generate_WhenTtlIsNotPositive_ReturnsValidationError(int minutes)
    {
        var generator = CreateGenerator();
        MediaAsset asset = CreateAsset(MediaAssetStatus.Approved);

        Result<MediaDeliveryUrl> result = generator.Generate(asset, TimeSpan.FromMinutes(minutes));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("MediaDelivery.TtlInvalid");
    }

    [Fact]
    public void Generate_WhenPublicBaseUrlHasTrailingSlash_NormalizesCorrectly()
    {
        var settings = new MediaDeliverySettings
        {
            SigningKey = _signingKeyBase64,
            PublicBaseUrl = "https://cdn.sensus.com///"
        };
        var generator = CreateGenerator(settings);
        MediaAsset asset = CreateAsset(MediaAssetStatus.Approved);

        Result<MediaDeliveryUrl> result = generator.Generate(asset, TimeSpan.FromMinutes(30));

        result.IsSuccess.Should().BeTrue();
        result.Value.Url.Should().StartWith($"https://cdn.sensus.com/api/v1/media/{asset.Id}/delivery");
    }
}
