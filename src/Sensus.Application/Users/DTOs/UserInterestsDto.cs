namespace Sensus.Application.Users.DTOs;

/// <summary>
/// Contrato público de intereses del usuario (Spec 054).
/// </summary>
public sealed record UserInterestsDto(IReadOnlyList<string> Categories);
