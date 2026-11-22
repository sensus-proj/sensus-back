namespace Sensus.Application.Common.Interfaces;

public sealed record TextModerationResult(
    bool IsApproved,
    string? Message = null,
    IReadOnlyList<string>? FlaggedCategories = null,
    string Decision = "Approved",
    IReadOnlyDictionary<string, double>? CategoryScores = null);

public interface ITextModerationService
{
    Task<TextModerationResult> ModerateTextAsync(
        string text,
        CancellationToken cancellationToken = default);

    Task<TextModerationResult> ModerateContentTextAsync(
        string text,
        string contentContext,
        CancellationToken cancellationToken = default) =>
        ModerateTextAsync(text, cancellationToken);
}
