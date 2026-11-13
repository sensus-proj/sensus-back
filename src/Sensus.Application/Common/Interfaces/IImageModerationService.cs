namespace Sensus.Application.Common.Interfaces;

public sealed record ImageModerationResult(
    bool IsApproved,
    string? Message = null,
    IReadOnlyList<string>? FlaggedCategories = null,
    string Decision = "Approved",
    IReadOnlyDictionary<string, double>? CategoryScores = null,
    string? AppliedThresholdSummary = null);

public interface IImageModerationService
{
    Task<ImageModerationResult> ModerateImageBytesAsync(
        byte[] imageBytes,
        string contentType = "image/jpeg",
        string? textContext = null,
        CancellationToken cancellationToken = default);
}
