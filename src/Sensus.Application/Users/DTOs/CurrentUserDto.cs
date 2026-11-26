namespace Sensus.Application.Users.DTOs;

/// <summary>
/// Datos de identidad de la sesión actual leídos desde los claims del JWT validado (FR-002-07).
/// </summary>
public sealed record CurrentUserDto(
    string? UserId,
    string? NombreCompleto,
    string? Correo,
    IReadOnlyList<string> Roles);
