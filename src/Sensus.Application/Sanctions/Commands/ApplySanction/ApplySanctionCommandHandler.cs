using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Common.Settings;
using Sensus.Application.Sanctions.Common;
using Sensus.Domain.Common;
using Sensus.Domain.Entities;
using Sensus.Domain.Enums;
using Sensus.Domain.Policies;
using Sensus.Domain.Repositories;
using Sensus.Domain.ValueObjects;

namespace Sensus.Application.Sanctions.Commands.ApplySanction;

public sealed class ApplySanctionCommandHandler : IRequestHandler<ApplySanctionCommand, Result>
{
    private const string AutomaticReason = "Contenido rechazado por la moderación automática (banda alta).";

    private readonly ISanctionRepository _sanctions;
    private readonly IUserRepository _users;
    private readonly ISanctionNotifier _notifier;
    private readonly ISanctionMetrics _metrics;
    private readonly IDateTimeProvider _clock;
    private readonly IOptions<SanctionSettings> _settings;
    private readonly ILogger<ApplySanctionCommandHandler> _logger;

    public ApplySanctionCommandHandler(
        ISanctionRepository sanctions,
        IUserRepository users,
        ISanctionNotifier notifier,
        ISanctionMetrics metrics,
        IDateTimeProvider clock,
        IOptions<SanctionSettings> settings,
        ILogger<ApplySanctionCommandHandler> logger)
    {
        _sanctions = sanctions;
        _users = users;
        _notifier = notifier;
        _metrics = metrics;
        _clock = clock;
        _settings = settings;
        _logger = logger;
    }

    public async Task<Result> Handle(ApplySanctionCommand request, CancellationToken cancellationToken)
    {
        SanctionSettings settings = _settings.Value;

        // D-3: solo la decisión configurada (Rejected) sanciona; Quarantine, incluida la falla de moderación, no.
        if (!settings.Enabled ||
            !string.Equals(request.Decision, settings.TriggerDecision, StringComparison.OrdinalIgnoreCase))
        {
            return Result.Success();
        }

        User? user = await _users.GetByIdAsync(request.UserId, cancellationToken);
        if (user is null)
        {
            return Result.Failure(Error.NotFound("Sanction.UserNotFound", "El usuario a sancionar no existe."));
        }

        // CA-020-10: una sanción automática nunca recae sobre un administrador.
        if (user.HasRole(RoleType.Administrador))
        {
            _logger.LogWarning("SecurityAudit SanctionSkipped Reason=AdminTarget UserId={UserId}", user.Id);
            return Result.Success();
        }

        DateTime now = _clock.UtcNow;

        if (request.MediaId is { } mediaId)
        {
            Sanction? existing = await _sanctions.GetByMediaIdAsync(mediaId, cancellationToken);
            if (existing is not null)
            {
                await EnsureSuspendedAsync(user, existing, now, cancellationToken);
                return Result.Success();
            }
        }

        int priorSanctions = await _sanctions.CountNotRevertedSinceAsync(
            user.Id,
            now.AddDays(-Math.Max(1, settings.EscalationWindowDays)),
            cancellationToken);

        SanctionStep step = SanctionEscalationPolicy.Resolve(priorSanctions, settings.ToSteps());

        var evidence = new SanctionEvidence(
            request.MediaId,
            request.Decision,
            request.Categories ?? [],
            request.Scores ?? new Dictionary<string, double>(),
            request.ThresholdSummary,
            "UserSuspended");

        Result<Sanction> created = Sanction.CreateAutomatic(user.Id, AutomaticReason, evidence, now, step.Hours);
        if (created.IsFailure)
        {
            return Result.Failure(created.Error);
        }

        Sanction sanction = created.Value;
        await _sanctions.AddAsync(sanction, cancellationToken);

        user.Suspend(sanction.Id, sanction.EndsAtUtc, now);
        await _users.UpdateAsync(user, cancellationToken);

        _metrics.SanctionApplied(automatic: true);
        _logger.LogInformation(
            "SecurityAudit SanctionApplied SanctionId={SanctionId} UserId={UserId} Type={Type} Permanent={Permanent}",
            sanction.Id,
            user.Id,
            sanction.Type,
            sanction.IsPermanent);

        await _notifier.NotifyAppliedAsync(sanction, cancellationToken);
        return Result.Success();
    }

    /// <summary>
    /// Repara la inconsistencia si la sanción se guardó pero el usuario no quedó suspendido (reintento idempotente).
    /// </summary>
    private async Task EnsureSuspendedAsync(
        User user,
        Sanction sanction,
        DateTime now,
        CancellationToken cancellationToken)
    {
        if (!sanction.IsActive(now) || user.Suspension?.SanctionId == sanction.Id)
        {
            return;
        }

        user.Suspend(sanction.Id, sanction.EndsAtUtc, now);
        await _users.UpdateAsync(user, cancellationToken);
        _logger.LogWarning("SecurityAudit SanctionRepaired SanctionId={SanctionId} UserId={UserId}", sanction.Id, user.Id);
    }
}
