using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Common.Settings;
using Sensus.Domain.Common;
using Sensus.Domain.Entities;
using Sensus.Domain.Enums;
using Sensus.Domain.Repositories;

namespace Sensus.Application.Moderation.Audit;

/// <summary>
/// Registra evidencia inmutable de cada decisión de moderación (Spec 021).
/// Ref: FR-021-01 - decisión, puntajes y umbral; FR-021-04 - retención configurada.
/// </summary>
public sealed class ModerationAuditRecorder : IModerationAuditRecorder
{
    /// <summary>Actor reservado para decisiones automáticas del sistema.</summary>
    public const string SystemActorId = "system";

    private readonly IModerationAuditRepository _repository;
    private readonly ICurrentUserService _currentUser;
    private readonly ICorrelationIdAccessor _correlation;
    private readonly IDateTimeProvider _clock;
    private readonly IOptions<ModerationAuditSettings> _settings;
    private readonly ILogger<ModerationAuditRecorder> _logger;

    public ModerationAuditRecorder(
        IModerationAuditRepository repository,
        ICurrentUserService currentUser,
        ICorrelationIdAccessor correlation,
        IDateTimeProvider clock,
        IOptions<ModerationAuditSettings> settings,
        ILogger<ModerationAuditRecorder> logger)
    {
        _repository = repository;
        _currentUser = currentUser;
        _correlation = correlation;
        _clock = clock;
        _settings = settings;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result> RecordAsync(ModerationAuditEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);

        ModerationActorType actorType = entry.ActorType ?? ResolveActorType();
        string actorId = string.IsNullOrWhiteSpace(entry.ActorId) ? ResolveActorId() : entry.ActorId.Trim();

        Result<ModerationAuditRecord> recordResult = ModerationAuditRecord.Create(
            entry.TargetId,
            entry.TargetType,
            entry.Verdict,
            entry.Decision,
            entry.ContentContext,
            entry.FlaggedCategories,
            entry.CategoryScores,
            entry.ThresholdApplied,
            actorType,
            actorId,
            _correlation.CorrelationId,
            _clock.UtcNow,
            _settings.Value.RetentionDays);

        if (recordResult.IsFailure)
        {
            _logger.LogError(
                "Evidencia de moderación inválida para {TargetType} {TargetId}: {Code}.",
                entry.TargetType,
                entry.TargetId,
                recordResult.Error.Code);
            return Result.Failure(recordResult.Error);
        }

        try
        {
            await _repository.AddAsync(recordResult.Value, cancellationToken);
            return Result.Success();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "No se pudo persistir la auditoría de moderación para {TargetType} {TargetId}.",
                entry.TargetType,
                entry.TargetId);
            return Result.Failure(
                Error.Unavailable(
                    "ModerationAudit.Unavailable",
                    "No se pudo registrar la auditoría de moderación."));
        }
    }

    /// <inheritdoc />
    public async Task RecordBestEffortAsync(ModerationAuditEntry entry, CancellationToken cancellationToken = default)
    {
        Result result = await RecordAsync(entry, cancellationToken);
        if (result.IsFailure)
        {
            _logger.LogError(
                "La auditoría de moderación no se persistió (mejor esfuerzo) para {TargetType} {TargetId}: {Code}.",
                entry.TargetType,
                entry.TargetId,
                result.Error.Code);
        }
    }

    private ModerationActorType ResolveActorType()
    {
        if (!_currentUser.IsAuthenticated || string.IsNullOrWhiteSpace(_currentUser.UserId))
        {
            return ModerationActorType.System;
        }

        return _currentUser.Roles.Contains(RoleType.Administrador, StringComparer.OrdinalIgnoreCase)
            ? ModerationActorType.Administrator
            : ModerationActorType.User;
    }

    private string ResolveActorId() =>
        string.IsNullOrWhiteSpace(_currentUser.UserId) ? SystemActorId : _currentUser.UserId.Trim();
}
