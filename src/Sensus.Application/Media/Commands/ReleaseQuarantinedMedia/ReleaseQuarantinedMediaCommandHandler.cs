using MediatR;
using Microsoft.Extensions.Logging;
using Sensus.Application.Common.Interfaces;
using Sensus.Domain.Common;
using Sensus.Domain.Entities;
using Sensus.Domain.Enums;
using Sensus.Domain.Repositories;

namespace Sensus.Application.Media.Commands.ReleaseQuarantinedMedia;

/// <summary>
/// Libera un activo multimedia de cuarentena tras revisión manual de un administrador (Spec 019 / Spec 021).
/// Ref: FR-021-01 - auditoría de la decisión administrativa previa a la persistencia (fail-closed, B4).
/// </summary>
public sealed class ReleaseQuarantinedMediaCommandHandler
    : IRequestHandler<ReleaseQuarantinedMediaCommand, Result<ReleasedMediaDto>>
{
    private readonly IMediaAssetRepository _mediaAssetRepository;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ILogger<ReleaseQuarantinedMediaCommandHandler> _logger;
    private readonly IModerationAuditRecorder _auditRecorder;

    public ReleaseQuarantinedMediaCommandHandler(
        IMediaAssetRepository mediaAssetRepository,
        ICurrentUserService currentUser,
        IDateTimeProvider dateTimeProvider,
        ILogger<ReleaseQuarantinedMediaCommandHandler> logger,
        IModerationAuditRecorder auditRecorder)
    {
        _mediaAssetRepository = mediaAssetRepository;
        _currentUser = currentUser;
        _dateTimeProvider = dateTimeProvider;
        _logger = logger;
        _auditRecorder = auditRecorder;
    }

    public async Task<Result<ReleasedMediaDto>> Handle(
        ReleaseQuarantinedMediaCommand request,
        CancellationToken cancellationToken)
    {
        if (request.MediaId == Guid.Empty)
        {
            return Result.Failure<ReleasedMediaDto>(
                Error.Validation("Media.IdInvalid", "El identificador del activo es obligatorio."));
        }

        MediaAsset? asset = await _mediaAssetRepository.GetByIdAsync(request.MediaId, cancellationToken);
        if (asset is null)
        {
            return Result.Failure<ReleasedMediaDto>(
                Error.NotFound("Media.NotFound", "No existe el activo multimedia indicado."));
        }

        // La regla de qué estados se pueden liberar vive en el dominio.
        if (!asset.Release())
        {
            return Result.Failure<ReleasedMediaDto>(
                Error.Conflict("Media.NotQuarantined", "Solo se puede liberar contenido que está en cuarentena."));
        }

        // B4: la auditoría va ANTES de UpdateAsync y con RecordAsync (fail-closed).
        // Si falla el registro de auditoría, el activo NO se libera.
        var auditEntry = new ModerationAuditEntry(
            asset.Id,
            ModerationTargetType.MediaAsset,
            ModerationVerdict.Approved,
            "Approved",
            "quarantine_release",
            FlaggedCategories: [],
            CategoryScores: null,
            ThresholdApplied: "manual_admin_release",
            ActorId: _currentUser.UserId,
            ActorType: ModerationActorType.Administrator);

        Result auditResult = await _auditRecorder.RecordAsync(auditEntry, cancellationToken);
        if (auditResult.IsFailure)
        {
            _logger.LogWarning(
                "Fallo al registrar la auditoría de moderación para liberar el activo {MediaId}. Operación cancelada.",
                asset.Id);
            return Result.Failure<ReleasedMediaDto>(auditResult.Error);
        }

        await _mediaAssetRepository.UpdateAsync(asset, cancellationToken);

        DateTime now = _dateTimeProvider.UtcNow;

        _logger.LogInformation(
            "Moderación manual: activo {MediaId} liberado de cuarentena por el revisor {ReviewerId}. Acción {Action}.",
            asset.Id,
            _currentUser.UserId,
            "Release");

        return Result.Success(new ReleasedMediaDto(asset.Id, asset.Status.ToString(), asset.SecureUrl, now));
    }
}
