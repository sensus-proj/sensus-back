using MediatR;
using Sensus.Domain.Common;

namespace Sensus.Application.Media.Commands.PurgeQuarantinedMedia;

/// <summary>
/// FR-019-03: elimina el contenido que superó la retención máxima en cuarentena.
/// La retención por defecto es de 30 días (NFR-019-01) y se configura desde el host.
/// </summary>
public sealed record PurgeQuarantinedMediaCommand(int RetentionDays = PurgeQuarantinedMediaCommand.DefaultRetentionDays)
    : IRequest<Result<PurgeQuarantinedMediaResultDto>>
{
    public const int DefaultRetentionDays = 30;

    /// <summary>Máximo de activos procesados por ejecución para acotar la duración de la tarea.</summary>
    public const int BatchSize = 100;
}

public sealed record PurgeQuarantinedMediaResultDto(int PurgedCount, int FailedCount, DateTime ExecutedAtUtc);
