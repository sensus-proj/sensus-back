using MediatR;
using Sensus.Domain.Common;

namespace Sensus.Application.Media.Commands.ReleaseQuarantinedMedia;

/// <summary>
/// FR-019-04: un administrador libera un activo en cuarentena tras revisarlo y aprobarlo.
/// </summary>
public sealed record ReleaseQuarantinedMediaCommand(Guid MediaId) : IRequest<Result<ReleasedMediaDto>>;

/// <summary>
/// Resultado de la liberación: el activo queda aprobado y expone su URL pública.
/// </summary>
public sealed record ReleasedMediaDto(Guid MediaId, string Status, string? SecureUrl, DateTime ReleasedAtUtc);
