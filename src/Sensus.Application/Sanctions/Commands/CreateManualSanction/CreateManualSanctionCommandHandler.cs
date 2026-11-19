using MediatR;
using Microsoft.Extensions.Logging;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Sanctions.Common;
using Sensus.Domain.Common;
using Sensus.Domain.Entities;
using Sensus.Domain.Enums;
using Sensus.Domain.Repositories;

namespace Sensus.Application.Sanctions.Commands.CreateManualSanction;

public sealed class CreateManualSanctionCommandHandler
    : IRequestHandler<CreateManualSanctionCommand, Result<SanctionDto>>
{
    private readonly ISanctionRepository _sanctions;
    private readonly IUserRepository _users;
    private readonly ICurrentUserService _currentUser;
    private readonly ISanctionNotifier _notifier;
    private readonly ISanctionMetrics _metrics;
    private readonly IDateTimeProvider _clock;
    private readonly ILogger<CreateManualSanctionCommandHandler> _logger;

    public CreateManualSanctionCommandHandler(
        ISanctionRepository sanctions,
        IUserRepository users,
        ICurrentUserService currentUser,
        ISanctionNotifier notifier,
        ISanctionMetrics metrics,
        IDateTimeProvider clock,
        ILogger<CreateManualSanctionCommandHandler> logger)
    {
        _sanctions = sanctions;
        _users = users;
        _currentUser = currentUser;
        _notifier = notifier;
        _metrics = metrics;
        _clock = clock;
        _logger = logger;
    }

    public async Task<Result<SanctionDto>> Handle(
        CreateManualSanctionCommand request,
        CancellationToken cancellationToken)
    {
        // Defensa en profundidad: la política HTTP ya exige el rol, pero el caso de uso no confía en el borde.
        if (!_currentUser.TryGetUserId(out Guid adminId) || !_currentUser.IsAdministrator())
        {
            return Result.Failure<SanctionDto>(
                Error.Forbidden("Sanction.AdminRequired", "Solo un administrador puede aplicar sanciones."));
        }

        if (request.UserId == adminId)
        {
            return Result.Failure<SanctionDto>(TargetNotAllowed());
        }

        User? target = await _users.GetByIdAsync(request.UserId, cancellationToken);
        if (target is null)
        {
            return Result.Failure<SanctionDto>(
                Error.NotFound("Sanction.UserNotFound", "El usuario a sancionar no existe."));
        }

        if (target.HasRole(RoleType.Administrador))
        {
            return Result.Failure<SanctionDto>(TargetNotAllowed());
        }

        DateTime now = _clock.UtcNow;
        Result<Sanction> created = Sanction.CreateManual(target.Id, adminId, request.Reason, now, request.DurationHours);
        if (created.IsFailure)
        {
            return Result.Failure<SanctionDto>(created.Error);
        }

        Sanction sanction = created.Value;
        await _sanctions.AddAsync(sanction, cancellationToken);

        target.Suspend(sanction.Id, sanction.EndsAtUtc, now);
        await _users.UpdateAsync(target, cancellationToken);

        _metrics.SanctionApplied(automatic: false);
        _logger.LogInformation(
            "SecurityAudit SanctionApplied SanctionId={SanctionId} UserId={UserId} Type={Type} Permanent={Permanent} Actor={Actor}",
            sanction.Id,
            target.Id,
            sanction.Type,
            sanction.IsPermanent,
            adminId);

        await _notifier.NotifyAppliedAsync(sanction, cancellationToken);
        return Result.Success(sanction.ToDto(now));
    }

    private static Error TargetNotAllowed() =>
        Error.Forbidden(
            "Sanction.TargetNotAllowed",
            "No puedes sancionarte a ti mismo ni a otro administrador.");
}
