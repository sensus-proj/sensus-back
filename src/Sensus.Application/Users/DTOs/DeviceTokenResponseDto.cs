namespace Sensus.Application.Users.DTOs;

public sealed record DeviceTokenResponseDto(
    string DeviceId,
    string Platform,
    string Token,
    bool IsActive,
    DateTime RegisteredAtUtc,
    string Message);
