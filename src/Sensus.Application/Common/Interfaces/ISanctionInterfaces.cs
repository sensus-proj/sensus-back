using Sensus.Domain.Entities;

namespace Sensus.Application.Common.Interfaces;

/// <summary>
/// Notifica al usuario cuando una sanción se aplica o se revierte (Spec 020, FR-020-08). Nunca lanza: un fallo
/// de notificación no debe impedir el bloqueo ni la reversión.
/// </summary>
public interface ISanctionNotifier
{
    Task NotifyAppliedAsync(Sanction sanction, CancellationToken cancellationToken = default);

    Task NotifyRevertedAsync(Sanction sanction, CancellationToken cancellationToken = default);
}

/// <summary>
/// Publica las métricas de sanciones (Spec 020, NFR-020-03). Sin dimensiones con datos personales.
/// </summary>
public interface ISanctionMetrics
{
    void SanctionApplied(bool automatic);

    void SanctionReverted();
}
