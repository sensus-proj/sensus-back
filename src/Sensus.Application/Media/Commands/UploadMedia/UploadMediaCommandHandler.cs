using MediatR;
using Microsoft.Extensions.Logging;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Common.Moderation;
using Sensus.Application.Media.Commands.UploadMedia;
using Sensus.Application.Media.DTOs;
using Sensus.Application.Sanctions.Commands.ApplySanction;
using Sensus.Domain.Common;
using Sensus.Domain.Entities;
using Sensus.Domain.Enums;
using Sensus.Domain.Repositories;
using IMediaAssetRepository = Sensus.Domain.Repositories.IMediaAssetRepository;

namespace Sensus.Application.Media.Commands.UploadMedia;

public sealed class UploadMediaCommandHandler : IRequestHandler<UploadMediaCommand, Result<UploadMediaResponseDto>>
{
    private readonly IMediaStorage _mediaStorage;
    private readonly IImageModerationService _moderationService;
    private readonly IMediaAssetRepository _mediaAssetRepository;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ISender _sender;
    private readonly ILogger<UploadMediaCommandHandler> _logger;
    private readonly IModerationAuditRecorder _auditRecorder;
    private readonly ICdnCacheInvalidator _cdnCacheInvalidator;
    private readonly IModerationEvidenceStore? _evidence;

    public UploadMediaCommandHandler(
        IMediaStorage mediaStorage,
        IImageModerationService moderationService,
        IMediaAssetRepository mediaAssetRepository,
        ICurrentUserService currentUser,
        IDateTimeProvider dateTimeProvider,
        ISender sender,
        ILogger<UploadMediaCommandHandler> logger,
        IModerationAuditRecorder auditRecorder,
        ICdnCacheInvalidator cdnCacheInvalidator,
        IModerationEvidenceStore? evidence = null)
    {
        _mediaStorage = mediaStorage;
        _moderationService = moderationService;
        _mediaAssetRepository = mediaAssetRepository;
        _currentUser = currentUser;
        _dateTimeProvider = dateTimeProvider;
        _sender = sender;
        _logger = logger;
        _auditRecorder = auditRecorder;
        _cdnCacheInvalidator = cdnCacheInvalidator;
        _evidence = evidence;
    }

    public async Task<Result<UploadMediaResponseDto>> Handle(
        UploadMediaCommand request,
        CancellationToken cancellationToken)
    {
        string? userId = _currentUser.UserId;
        if (string.IsNullOrWhiteSpace(userId) || !Guid.TryParse(userId, out Guid parsedUserId))
        {
            return Result.Failure<UploadMediaResponseDto>(
                Error.Unauthorized("Media.Unauthenticated", "Usuario no autenticado."));
        }

        // Verificar límite diario de 20 archivos por usuario (FR-022-07)
        DateTime todayUtc = _dateTimeProvider.UtcNow.Date;
        int uploadsToday = await _mediaAssetRepository.CountByUserAndDateAsync(parsedUserId, todayUtc, cancellationToken);
        if (uploadsToday >= 20)
        {
            return Result.Failure<UploadMediaResponseDto>(
                Error.Validation("Media.DailyLimitExceeded", "Se ha alcanzado el límite de 20 cargas por usuario y día."));
        }

        // Subir a Cloudinary de forma privada (hasta aprobación) (FR-022-04)
        Result<MediaUploadResult> uploadResult = await _mediaStorage.UploadImageAsync(
            request.ImageBytes,
            request.ContentType,
            parsedUserId,
            cancellationToken);

        if (uploadResult.IsFailure)
        {
            return Result.Failure<UploadMediaResponseDto>(uploadResult.Error);
        }

        // Moderar la imagen antes de exponerla públicamente (FR-022-06)
        ImageModerationResult moderationResult = await _moderationService.ModerateImageBytesAsync(
            request.ImageBytes,
            request.ContentType,
            request.TextContext,
            cancellationToken);

        MediaAssetStatus status = moderationResult is { IsApproved: true, Decision: "Approved" }
            ? MediaAssetStatus.Approved
            : moderationResult.Decision == "Quarantine"
                ? MediaAssetStatus.Quarantined
                : MediaAssetStatus.Rejected;

        // FR-019-02: el contenido rechazado se elimina del almacenamiento y no conserva URL; el que queda en
        // cuarentena conserva la URL solo de forma interna para una eventual liberación (FR-019-04).
        MediaAsset mediaAsset = MediaAsset.Create(
            parsedUserId,
            uploadResult.Value.PublicId,
            status == MediaAssetStatus.Rejected ? null : uploadResult.Value.SecureUrl,
            request.ContentType,
            status,
            _dateTimeProvider.UtcNow,
            status == MediaAssetStatus.Approved && !string.IsNullOrWhiteSpace(request.TextContext)
                ? ModeratedTextFingerprint.Create(request.TextContext)
                : null);

        if (_evidence is not null)
        {
            await _evidence.AppendAsync(new ModerationEvidence(
                mediaAsset.Id, "MediaAsset", moderationResult.Decision, moderationResult.FlaggedCategories,
                moderationResult.CategoryScores, moderationResult.AppliedThresholdSummary,
                status == MediaAssetStatus.Approved ? "Allow" : "Block", mediaAsset.CreatedAtUtc), cancellationToken);
        }

        // Spec 021 (FR-021-01): la evidencia se registra antes de publicar. Si no se puede persistir, el
        // contenido no se publica (fail-closed).
        ModerationAuditEntry auditEntry = new(
            mediaAsset.Id,
            ModerationTargetType.MediaAsset,
            ToVerdict(status),
            moderationResult.Decision,
            "media_upload",
            moderationResult.FlaggedCategories,
            moderationResult.CategoryScores,
            moderationResult.AppliedThresholdSummary);

        if (status == MediaAssetStatus.Rejected)
        {
            await _auditRecorder.RecordBestEffortAsync(auditEntry, cancellationToken);
            await _mediaStorage.DeleteImageAsync(uploadResult.Value.PublicId, cancellationToken);
            await _cdnCacheInvalidator.InvalidateAsync(uploadResult.Value.PublicId, cancellationToken);
        }
        else
        {
            Result auditResult = await _auditRecorder.RecordAsync(auditEntry, cancellationToken);
            if (auditResult.IsFailure)
            {
                await _mediaStorage.DeleteImageAsync(uploadResult.Value.PublicId, cancellationToken);
                await _cdnCacheInvalidator.InvalidateAsync(uploadResult.Value.PublicId, cancellationToken);
                return Result.Failure<UploadMediaResponseDto>(auditResult.Error);
            }
        }

        await _mediaAssetRepository.AddAsync(mediaAsset, cancellationToken);

        await TrySanctionAsync(parsedUserId, mediaAsset.Id, moderationResult, cancellationToken);

        UploadMediaResponseDto dto = new UploadMediaResponseDto(
            MediaId: mediaAsset.Id,
            PublicId: mediaAsset.PublicId,
            SecureUrl: mediaAsset.Status == MediaAssetStatus.Approved ? mediaAsset.SecureUrl : null,
            Status: mediaAsset.Status.ToString(),
            ContentType: mediaAsset.ContentType,
            ModerationDecision: moderationResult.Decision,
            ModerationMessage: moderationResult.Message ?? string.Empty,
            FlaggedCategories: moderationResult.FlaggedCategories,
            CreatedAtUtc: mediaAsset.CreatedAtUtc);

        return Result.Success(dto);
    }

    /// <summary>
    /// Spec 021: traduce el estado del activo al veredicto auditado.
    /// </summary>
    private static ModerationVerdict ToVerdict(MediaAssetStatus status) =>
        status switch
        {
            MediaAssetStatus.Approved => ModerationVerdict.Approved,
            MediaAssetStatus.Quarantined => ModerationVerdict.Quarantined,
            _ => ModerationVerdict.Rejected
        };

    /// <summary>
    /// Spec 020 (FR-020-01): una decisión de banda alta dispara la sanción. Un fallo al sancionar nunca debe
    /// alterar el veredicto de moderación ya aplicado ni publicar el contenido (fail-closed).
    /// </summary>
    private async Task TrySanctionAsync(
        Guid userId,
        Guid mediaId,
        ImageModerationResult moderation,
        CancellationToken cancellationToken)
    {
        if (moderation.IsApproved)
        {
            return;
        }

        try
        {
            await _sender.Send(
                new ApplySanctionCommand(
                    userId,
                    mediaId,
                    moderation.Decision,
                    moderation.FlaggedCategories,
                    moderation.CategoryScores,
                    moderation.AppliedThresholdSummary),
                cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "No se pudo aplicar la sanción para el activo {MediaId}; el contenido sigue sin publicarse.", mediaId);
        }
    }
}
