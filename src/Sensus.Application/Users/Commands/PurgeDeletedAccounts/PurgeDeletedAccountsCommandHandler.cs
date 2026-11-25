using MediatR;
using Microsoft.Extensions.Logging;
using Sensus.Application.Common.Interfaces;
using Sensus.Domain.Common;
using Sensus.Domain.Repositories;

namespace Sensus.Application.Users.Commands.PurgeDeletedAccounts;

/// <summary>
/// Elimina únicamente las cuentas con <c>DeletedAtUtc &lt;= cutoff</c>. La eliminación es condicional
/// en la base de datos, por lo que no purga cuentas activas y es idempotente ante reintentos.
/// </summary>
public sealed class PurgeDeletedAccountsCommandHandler
    : IRequestHandler<PurgeDeletedAccountsCommand, Result<PurgeDeletedAccountsResult>>
{
    /// <summary>Límite de documentos por ejecución para evitar recorridos ilimitados (NFR-060-02).</summary>
    public const int BatchSize = 100;

    private readonly IUserRepository _userRepository;
    private readonly ILogger<PurgeDeletedAccountsCommandHandler>? _logger;
    private readonly ICorrelationIdAccessor? _correlationIdAccessor;

    public PurgeDeletedAccountsCommandHandler(
        IUserRepository userRepository,
        ILogger<PurgeDeletedAccountsCommandHandler>? logger = null,
        ICorrelationIdAccessor? correlationIdAccessor = null)
    {
        _userRepository = userRepository;
        _logger = logger;
        _correlationIdAccessor = correlationIdAccessor;
    }

    public async Task<Result<PurgeDeletedAccountsResult>> Handle(
        PurgeDeletedAccountsCommand request,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<Guid> candidates = await _userRepository.GetIdsDeletedBeforeAsync(
            request.CutoffUtc,
            BatchSize,
            cancellationToken);

        int purgedCount = 0;
        foreach (Guid userId in candidates)
        {
            bool purged = await _userRepository.PurgeDeletedAsync(userId, request.CutoffUtc, cancellationToken);
            if (!purged)
            {
                continue;
            }

            purgedCount++;
            _logger?.LogInformation(
                "SecurityAudit DeletedAccountPurged UserId={UserId} Result=Success CorrelationId={CorrelationId}",
                userId,
                _correlationIdAccessor?.CorrelationId ?? "unknown");
        }

        return Result.Success(new PurgeDeletedAccountsResult(purgedCount));
    }
}
