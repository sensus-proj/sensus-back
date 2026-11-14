using FluentAssertions;
using Sensus.Application.Media.Commands.UploadMedia;
using Xunit;

namespace Sensus.Application.UnitTests.Media.Commands.UploadMedia;

public sealed class UploadMediaCommandValidatorTests
{
    private readonly UploadMediaCommandValidator _validator = new();

    [Fact]
    public void Validate_WithValidJpeg_ShouldPass()
    {
        // Arrange
        var jpegBytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10 };
        var command = new UploadMediaCommand(jpegBytes, "image/jpeg");

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithValidPng_ShouldPass()
    {
        // Arrange
        var pngBytes = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A };
        var command = new UploadMediaCommand(pngBytes, "image/png");

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithValidWebp_ShouldPass()
    {
        // Arrange
        var webpBytes = new byte[] { 0x52, 0x49, 0x46, 0x46, 0x00, 0x00, 0x00, 0x00, 0x57, 0x45, 0x42, 0x50 };
        var command = new UploadMediaCommand(webpBytes, "image/webp");

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithEmptyImageBytes_ShouldFail()
    {
        // Arrange
        var command = new UploadMediaCommand([], "image/jpeg");

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(UploadMediaCommand.ImageBytes));
    }

    [Fact]
    public void Validate_WithNullImageBytes_ShouldFail()
    {
        // Arrange
        var command = new UploadMediaCommand(null!, "image/jpeg");

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(UploadMediaCommand.ImageBytes));
    }

    [Fact]
    public void Validate_WithFileOver4_5MB_ShouldFail()
    {
        // Arrange
        var largeBytes = new byte[4_500_001]; // Un byte más de 4,5 MB
        largeBytes[0] = 0xFF;
        largeBytes[1] = 0xD8;
        largeBytes[2] = 0xFF;
        var command = new UploadMediaCommand(largeBytes, "image/jpeg");

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(UploadMediaCommand.ImageBytes));
    }

    [Theory]
    [InlineData("image/gif")]
    [InlineData("image/bmp")]
    [InlineData("image/svg+xml")]
    [InlineData("application/pdf")]
    [InlineData("text/plain")]
    public void Validate_WithInvalidContentType_ShouldFail(string contentType)
    {
        // Arrange
        var jpegBytes = new byte[] { 0xFF, 0xD8, 0xFF };
        var command = new UploadMediaCommand(jpegBytes, contentType);

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(UploadMediaCommand.ContentType));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_WithEmptyContentType_ShouldFail(string emptyContentType)
    {
        // Arrange
        var jpegBytes = new byte[] { 0xFF, 0xD8, 0xFF };
        var command = new UploadMediaCommand(jpegBytes, emptyContentType);

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(UploadMediaCommand.ContentType));
    }

    [Fact]
    public void Validate_WithInvalidImageSignatureButValidContentType_ShouldFail()
    {
        // Arrange
        var exeBytes = new byte[] { 0x4D, 0x5A, 0x90, 0x00 }; // "MZ" header de Windows EXE
        var command = new UploadMediaCommand(exeBytes, "image/jpeg");

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(UploadMediaCommand.ImageBytes));
    }

    [Fact]
    public void Validate_WithTextContext_ShouldPass()
    {
        // Arrange
        var jpegBytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10 };
        var command = new UploadMediaCommand(jpegBytes, "image/jpeg", "Oferta de producto en buen estado");

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeTrue();
    }
}
