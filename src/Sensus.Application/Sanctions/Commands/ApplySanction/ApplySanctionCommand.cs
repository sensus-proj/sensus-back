using MediatR;
using Sensus.Domain.Common;

namespace Sensus.Application.Sanctions.Commands.ApplySanction;

/// <summary>
/// FR-020-01: aplica una sanción automática ante una decisión de moderación de banda alta. Es idempotente por
/// activo: una segunda invocación para el mismo <see cref="MediaId"/> no crea otra sanción.
/// </summary>
public sealed record ApplySanctionCommand(
    Guid UserId,
    Guid? MediaId,
    string Decision,
    IReadOnlyList<string>? Categories,
    IReadOnlyDictionary<string, double>? Scores,
    string? ThresholdSummary) : IRequest<Result>;
