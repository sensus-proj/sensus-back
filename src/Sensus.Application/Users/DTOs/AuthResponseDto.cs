namespace Sensus.Application.Users.DTOs;

public sealed record AuthResponseDto(
    string Token,
    string TokenType,
    DateTime ExpiresAtUtc,
    UserDto User,
    string? RefreshToken = null,
    DateTime? RefreshExpiresAtUtc = null,
    bool RequiresMfa = false,
    string? MfaChallengeToken = null);
