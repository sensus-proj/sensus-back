using FluentAssertions;
using NSubstitute;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Media.Commands.ModerateImage;
using Xunit;

namespace Sensus.Application.UnitTests.Media.Commands;

public sealed class ModerateImageCommandHandlerTests
{
    private readonly IImageModerationService _moderationService = Substitute.For<IImageModerationService>();
    private readonly ModerateImageCommandHandler _handler;

    public ModerateImageCommandHandlerTests()
    {
        _handler = new ModerateImageCommandHandler(_moderationService);
    }

    [Fact]
    public async Task Handle_WhenImageIsApproved_ReturnsApprovedResult()
    {
        // Arrange
        var testBytes = new byte[] { 1, 2, 3, 4 };
        var base64 = Convert.ToBase64String(testBytes);
        var command = new ModerateImageCommand(base64);

        _moderationService.ModerateImageBytesAsync(
                Arg.Any<byte[]>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(new ImageModerationResult(true, "Aprobada"));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Approved.Should().BeTrue();
        result.Value.Message.Should().Be("Aprobada");
    }

    private static readonly string[] ExpectedFlaggedCategories = ["hate"];

    [Fact]
    public async Task Handle_WhenImageIsFlagged_ReturnsRejectedResultWithReason()
    {
        // Arrange
        var testBytes = new byte[] { 10, 20, 30 };
        var base64 = Convert.ToBase64String(testBytes);
        var command = new ModerateImageCommand(base64);

        _moderationService.ModerateImageBytesAsync(
                Arg.Any<byte[]>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(new ImageModerationResult(false, "No se pudo subir la imagen por políticas de seguridad.", ExpectedFlaggedCategories));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Approved.Should().BeFalse();
        result.Value.Message.Should().Be("No se pudo subir la imagen por políticas de seguridad.");
        result.Value.FlaggedCategories.Should().Contain("hate");
    }

    [Fact]
    public async Task Handle_WhenDataUrlPrefixIsIncluded_StripsPrefixAndProcessesBytes()
    {
        // Arrange
        var testBytes = new byte[] { 5, 6, 7 };
        var base64 = $"data:image/png;base64,{Convert.ToBase64String(testBytes)}";
        var command = new ModerateImageCommand(base64, "image/png");

        _moderationService.ModerateImageBytesAsync(
                Arg.Is<byte[]>(b => b.SequenceEqual(testBytes)),
                Arg.Is("image/png"),
                Arg.Any<CancellationToken>())
            .Returns(new ImageModerationResult(true));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Approved.Should().BeTrue();
    }
}
