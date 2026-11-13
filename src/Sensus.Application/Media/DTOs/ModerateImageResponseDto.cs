namespace Sensus.Application.Media.DTOs;

public sealed record ModerateImageResponseDto(
    bool Approved,
    string Message,
    IReadOnlyList<string>? FlaggedCategories = null,
    string Decision = "Approved",
    IReadOnlyDictionary<string, double>? CategoryScores = null);
