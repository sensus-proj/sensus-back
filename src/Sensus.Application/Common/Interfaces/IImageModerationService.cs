namespace Sensus.Application.Common.Interfaces;

public sealed record ImageModerationResult(
    bool IsApproved,
    string? Message = null,
    IReadOnlyList<string>? FlaggedCategories = null);

public interface IImageModerationService
{
    Task<ImageModerationResult> ModerateImageBytesAsync(
        byte[] imageBytes,
        string contentType = "image/jpeg",
        CancellationToken cancellationToken = default);
}
