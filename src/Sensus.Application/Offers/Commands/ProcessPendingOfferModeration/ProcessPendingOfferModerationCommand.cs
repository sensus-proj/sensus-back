using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Common.Moderation;
using Sensus.Application.Common.Settings;
using Sensus.Application.Moderation.Audit;
using Sensus.Application.Offers.Notifications;
using Sensus.Domain.Common;
using Sensus.Domain.Entities;
using Sensus.Domain.Enums;
using Sensus.Domain.Repositories;

namespace Sensus.Application.Offers.Commands.ProcessPendingOfferModeration;

/// <summary>
/// Re-evalúa ofertas en <c>PendingModeration</c> cuyo próximo intento ya venció (Spec 034, FR-034-07).
/// Al aprobar publica, audita e invalida caché/notifica; ante rechazo explícito o agotamiento de intentos
/// rechaza sin exponer públicamente.
/// </summary>
public sealed record ProcessPendingOfferModerationCommand
    : IRequest<Result<ProcessPendingOfferModerationResultDto>>;

public sealed record ProcessPendingOfferModerationResultDto(
    int CheckedCount,
    int PublishedCount,
    int RejectedCount,
    int RescheduledCount);

public sealed class ProcessPendingOfferModerationCommandHandler
    : IRequestHandler<ProcessPendingOfferModerationCommand, Result<ProcessPendingOfferModerationResultDto>>
{
    private readonly IOfferRepository _offerRepository;
    private readonly IComercioRepository _comercioRepository;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IModerationAuditRecorder _auditRecorder;
    private readonly ILogger<ProcessPendingOfferModerationCommandHandler> _logger;
    private readonly ITextModerationService? _textModerationService;
    private readonly IMediaAssetRepository? _mediaAssets;
    private readonly ICacheService? _cacheService;
    private readonly INearbyOfferNotifier? _nearbyOfferNotifier;
    private readonly IOptions<OfferModerationRetrySettings>? _retrySettings;
    private readonly IOptions<OfferLimitSettings>? _limitSettings;

    public ProcessPendingOfferModerationCommandHandler(
        IOfferRepository offerRepository,
        IComercioRepository comercioRepository,
        IDateTimeProvider dateTimeProvider,
        IModerationAuditRecorder auditRecorder,
        ILogger<ProcessPendingOfferModerationCommandHandler> logger,
        ITextModerationService? textModerationService = null,
        IMediaAssetRepository? mediaAssets = null,
        ICacheService? cacheService = null,
        INearbyOfferNotifier? nearbyOfferNotifier = null,
        IOptions<OfferModerationRetrySettings>? retrySettings = null,
        IOptions<OfferLimitSettings>? limitSettings = null)
    {
        _offerRepository = offerRepository;
        _comercioRepository = comercioRepository;
        _dateTimeProvider = dateTimeProvider;
        _auditRecorder = auditRecorder;
        _logger = logger;
        _textModerationService = textModerationService;
        _mediaAssets = mediaAssets;
        _cacheService = cacheService;
        _nearbyOfferNotifier = nearbyOfferNotifier;
        _retrySettings = retrySettings;
        _limitSettings = limitSettings;
    }

    public async Task<Result<ProcessPendingOfferModerationResultDto>> Handle(
        ProcessPendingOfferModerationCommand request,
        CancellationToken cancellationToken)
    {
        DateTime now = _dateTimeProvider.UtcNow;
        OfferModerationRetrySettings settings = _retrySettings?.Value ?? new OfferModerationRetrySettings();
        int maxAttempts = Math.Max(1, settings.MaxAttempts);
        int retryDelaySeconds = Math.Max(5, settings.RetryDelaySeconds);
        int batchSize = Math.Clamp(settings.BatchSize, 1, 100);

        IReadOnlyList<Offer> dueOffers = await _offerRepository.GetPendingModerationDueAsync(
            now,
            batchSize,
            cancellationToken);

        int published = 0;
        int rejected = 0;
        int rescheduled = 0;

        foreach (Offer offer in dueOffers)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            if (await RejectIfViolatesPreconditionsAsync(offer, now, cancellationToken))
            {
                rejected++;
                continue;
            }

            ModerationEvaluation evaluation = await ContentModerationHelper.EvaluateAsync(
                _textModerationService,
                $"Título: {offer.Title}\nDescripción: {offer.Description}\nCategoría: {offer.Category ?? string.Empty}",
                "Publicación de producto u oferta comercial",
                "Offer.ContentModerationRejected",
                "El título o descripción de la publicación contiene lenguaje antisonante o prohibido.",
                cancellationToken);

            if (evaluation.IsApproved)
            {
                ModerationAuditEntry approveAudit = new(
                    offer.Id,
                    ModerationTargetType.Offer,
                    ModerationVerdict.Approved,
                    evaluation.Decision,
                    "offer_remoderation_approve",
                    evaluation.FlaggedCategories,
                    evaluation.CategoryScores,
                    ActorId: ModerationAuditRecorder.SystemActorId,
                    ActorType: ModerationActorType.System);

                Result auditResult = await _auditRecorder.RecordAsync(approveAudit, cancellationToken);
                if (auditResult.IsFailure)
                {
                    // Fail-closed: no se publica sin auditoría persistida; se reintentará en el siguiente ciclo.
                    rescheduled++;
                    continue;
                }

                Result approveResult = offer.ApproveFromModeration(now);
                if (approveResult.IsFailure)
                {
                    continue;
                }

                await _offerRepository.UpdateAsync(offer, cancellationToken);

                if (_cacheService is not null)
                {
                    await _cacheService.InvalidateAsync(offer.Id, cancellationToken);
                }

                if (offer.Status == OfferStatus.Published)
                {
                    published++;
                    await TryNotifyNearbyAsync(offer, cancellationToken);
                }

                continue;
            }

            if (evaluation.Verdict == ModerationVerdict.Rejected)
            {
                ModerationAuditEntry rejectAudit = new(
                    offer.Id,
                    ModerationTargetType.Offer,
                    ModerationVerdict.Rejected,
                    evaluation.Decision,
                    "offer_remoderation_reject",
                    evaluation.FlaggedCategories,
                    evaluation.CategoryScores,
                    ActorId: ModerationAuditRecorder.SystemActorId,
                    ActorType: ModerationActorType.System);

                await _auditRecorder.RecordBestEffortAsync(rejectAudit, cancellationToken);
                offer.RejectFromModeration(now);
                await _offerRepository.UpdateAsync(offer, cancellationToken);
                rejected++;
                continue;
            }

            // Aún en Quarantine / proveedor no disponible: reprogramar o rechazar al agotar intentos.
            offer.ScheduleModerationRetry(now, now.AddSeconds(retryDelaySeconds), maxAttempts);
            if (offer.Status == OfferStatus.Rejected)
            {
                ModerationAuditEntry exhaustedAudit = new(
                    offer.Id,
                    ModerationTargetType.Offer,
                    ModerationVerdict.Rejected,
                    "MaxRetryAttemptsExceeded",
                    "offer_remoderation_exhausted",
                    evaluation.FlaggedCategories,
                    evaluation.CategoryScores,
                    ActorId: ModerationAuditRecorder.SystemActorId,
                    ActorType: ModerationActorType.System);

                await _auditRecorder.RecordBestEffortAsync(exhaustedAudit, cancellationToken);
                rejected++;
            }
            else
            {
                rescheduled++;
            }

            await _offerRepository.UpdateAsync(offer, cancellationToken);
        }

        return Result.Success(new ProcessPendingOfferModerationResultDto(
            dueOffers.Count,
            published,
            rejected,
            rescheduled));
    }

    private async Task<bool> RejectIfViolatesPreconditionsAsync(
        Offer offer,
        DateTime now,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(offer.ImageUrl) && _mediaAssets is not null)
        {
            Comercio? merchant = await _comercioRepository.GetByIdAsync(offer.MerchantId, cancellationToken);
            string fingerprint = ModeratedTextFingerprint.Create($"{offer.Title}\n{offer.Description}");
            if (merchant is null
                || !await _mediaAssets.HasApprovedImageAsync(merchant.UsuarioId, offer.ImageUrl, fingerprint, cancellationToken))
            {
                await RejectWithAuditAsync(offer, now, "ImageNotApproved", "offer_remoderation_image_invalid", cancellationToken);
                return true;
            }
        }

        int maxActive = _limitSettings?.Value.MaxActivePerMerchant ?? OfferLimitSettings.DefaultMaxActivePerMerchant;
        long activeCount = await _offerRepository.CountActiveByMerchantAsync(offer.MerchantId, now, cancellationToken);
        if (activeCount >= maxActive)
        {
            await RejectWithAuditAsync(offer, now, "ActiveLimitReached", "offer_remoderation_limit_reached", cancellationToken);
            return true;
        }

        return false;
    }

    private async Task RejectWithAuditAsync(
        Offer offer,
        DateTime now,
        string decision,
        string context,
        CancellationToken cancellationToken)
    {
        ModerationAuditEntry entry = new(
            offer.Id,
            ModerationTargetType.Offer,
            ModerationVerdict.Rejected,
            decision,
            context,
            ActorId: ModerationAuditRecorder.SystemActorId,
            ActorType: ModerationActorType.System);

        await _auditRecorder.RecordBestEffortAsync(entry, cancellationToken);
        offer.RejectFromModeration(now);
        await _offerRepository.UpdateAsync(offer, cancellationToken);
    }

    private async Task TryNotifyNearbyAsync(Offer offer, CancellationToken cancellationToken)
    {
        if (_nearbyOfferNotifier is null)
        {
            return;
        }

        try
        {
            Comercio? merchant = await _comercioRepository.GetByIdAsync(offer.MerchantId, cancellationToken);
            if (merchant is null || !merchant.Activo)
            {
                return;
            }

            await _nearbyOfferNotifier.NotifyAsync(offer, merchant.UsuarioId, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error al notificar usuarios cercanos tras aprobar la oferta {OfferId}.", offer.Id);
        }
    }
}
