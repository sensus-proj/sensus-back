using MediatR;
using Sensus.Domain.Common;

namespace Sensus.Application.Users.Commands.PurgeDeletedAccounts;

/// <summary>
/// Purga de cuentas dadas de baja cuya retención venció (Spec 060, FR-060-04 y FR-060-06).
/// El <c>CutoffUtc</c> se calcula como <c>ahora - retención</c> con un reloj inyectado.
/// </summary>
public sealed record PurgeDeletedAccountsCommand(DateTime CutoffUtc)
    : IRequest<Result<PurgeDeletedAccountsResult>>;

/// <summary>Resultado de una ejecución de purga de cuentas eliminadas lógicamente.</summary>
public sealed record PurgeDeletedAccountsResult(int PurgedCount);
