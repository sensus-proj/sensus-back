namespace Sensus.Application.Media.DTOs;

public sealed record UploadMediaResponseDto(
    Guid MediaId,
    string PublicId,
    string? SecureUrl,
    string Status,
    string ContentType,
    string ModerationDecision,
    string ModerationMessage,
    IReadOnlyList<string>? FlaggedCategories = null,
    DateTime CreatedAtUtc = default);
