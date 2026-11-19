using MediatR;
using Microsoft.Extensions.Logging;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Sanctions.Common;
using Sensus.Domain.Common;
using Sensus.Domain.Entities;
using Sensus.Domain.Repositories;

namespace Sensus.Application.Sanctions.Commands.RevertSanction;

public sealed class RevertSanctionCommandHandler : IRequestHandler<RevertSanctionCommand, Result<SanctionDto>>
{
    private readonly ISanctionRepository _sanctions;
    private readonly IUserRepository _users;
    private readonly ICurrentUserService _currentUser;
    private readonly ISanctionNotifier _notifier;
    private readonly ISanctionMetrics _metrics;
    private readonly IDateTimeProvider _clock;
    private readonly ILogger<RevertSanctionCommandHandler> _logger;

    public RevertSanctionCommandHandler(
        ISanctionRepository sanctions,
        IUserRepository users,
        ICurrentUserService currentUser,
        ISanctionNotifier notifier,
        ISanctionMetrics metrics,
        IDateTimeProvider clock,
        ILogger<RevertSanctionCommandHandler> logger)
    {
        _sanctions = sanctions;
        _users = users;
        _currentUser = currentUser;
        _notifier = notifier;
        _metrics = metrics;
        _clock = clock;
        _logger = logger;
    }

    public async Task<Result<SanctionDto>> Handle(RevertSanctionCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.TryGetUserId(out Guid adminId) || !_currentUser.IsAdministrator())
        {
            return Result.Failure<SanctionDto>(
                Error.Forbidden("Sanction.AdminRequired", "Solo un administrador puede revertir sanciones."));
        }

        Sanction? sanction = await _sanctions.GetByIdAsync(request.SanctionId, cancellationToken);
        if (sanction is null)
        {
            return Result.Failure<SanctionDto>(
                Error.NotFound("Sanction.NotFound", "La sanción indicada no existe."));
        }

        DateTime now = _clock.UtcNow;
        Result reverted = sanction.Revert(adminId, request.Reason, now);
        if (reverted.IsFailure)
        {
            return Result.Failure<SanctionDto>(reverted.Error);
        }

        await _sanctions.UpdateAsync(sanction, cancellationToken);
        await RestoreUserAccessAsync(sanction, now, cancellationToken);

        _metrics.SanctionReverted();
        _logger.LogInformation(
            "SecurityAudit SanctionReverted SanctionId={SanctionId} UserId={UserId} Actor={Actor}",
            sanction.Id,
            sanction.UserId,
            adminId);

        await _notifier.NotifyRevertedAsync(sanction, cancellationToken);
        return Result.Success(sanction.ToDto(now));
    }

    /// <summary>
    /// Levanta la suspensión del usuario sin tocar sus datos (NFR-020-02). Si conserva otra sanción activa, la
    /// más severa pasa a ser su suspensión vigente.
    /// </summary>
    private async Task RestoreUserAccessAsync(Sanction sanction, DateTime now, CancellationToken cancellationToken)
    {
        User? user = await _users.GetByIdAsync(sanction.UserId, cancellationToken);
        if (user is null || !user.LiftSuspension(sanction.Id))
        {
            return;
        }

        IReadOnlyList<Sanction> remaining = await _sanctions.GetActiveByUserAsync(user.Id, now, cancellationToken);
        Sanction? strongest = remaining
            .OrderByDescending(s => s.IsPermanent)
            .ThenByDescending(s => s.EndsAtUtc)
            .FirstOrDefault();

        if (strongest is not null)
        {
            user.RestoreSuspension(strongest.Id, strongest.EndsAtUtc);
        }

        await _users.UpdateAsync(user, cancellationToken);
    }
}
