using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Common.Moderation;
using Sensus.Application.Common.Settings;
using Sensus.Application.Offers.DTOs;
using Sensus.Domain.Common;
using Sensus.Domain.Entities;
using Sensus.Domain.Enums;
using Sensus.Domain.Repositories;

namespace Sensus.Application.Offers.Commands.RepublishOffer;

public sealed class RepublishOfferCommandHandler : IRequestHandler<RepublishOfferCommand, Result<OfferDto>>
{
    private readonly IOfferRepository _offerRepository;
    private readonly IComercioRepository _comercioRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ILogger<RepublishOfferCommandHandler> _logger;
    private readonly ICacheService? _cacheService;
    private readonly ITextModerationService? _textModerationService;
    private readonly IOfferStatusHistoryRepository? _statusHistoryRepository;
    private readonly IModerationAuditRecorder _auditRecorder;
    private readonly IOptions<OfferLimitSettings>? _limitSettings;

    public RepublishOfferCommandHandler(
        IOfferRepository offerRepository,
        IComercioRepository comercioRepository,
        ICurrentUserService currentUserService,
        IDateTimeProvider dateTimeProvider,
        ILogger<RepublishOfferCommandHandler> logger,
        IModerationAuditRecorder auditRecorder,
        ICacheService? cacheService = null,
        ITextModerationService? textModerationService = null,
        IOfferStatusHistoryRepository? statusHistoryRepository = null,
        IOptions<OfferLimitSettings>? limitSettings = null)
    {
        _offerRepository = offerRepository;
        _comercioRepository = comercioRepository;
        _currentUserService = currentUserService;
        _dateTimeProvider = dateTimeProvider;
        _logger = logger;
        _auditRecorder = auditRecorder;
        _cacheService = cacheService;
        _textModerationService = textModerationService;
        _statusHistoryRepository = statusHistoryRepository;
        _limitSettings = limitSettings;
    }

    public async Task<Result<OfferDto>> Handle(RepublishOfferCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_currentUserService.UserId) || !Guid.TryParse(_currentUserService.UserId, out var userId))
        {
            return Result.Failure<OfferDto>(Error.Unauthorized("Offer.Unauthorized", "Debe iniciar sesión para republicar ofertas."));
        }

        Comercio? comercio = await _comercioRepository.GetByUsuarioIdAsync(userId, cancellationToken);
        if (comercio is null || !comercio!.Activo)
        {
            return Result.Failure<OfferDto>(Error.Forbidden("Offer.NoActiveMerchant", "El usuario no tiene un comercio activo registrado."));
        }

        Offer? offer = await _offerRepository.GetByIdAsync(request.OfferId, cancellationToken);
        if (offer is null)
        {
            return Result.Failure<OfferDto>(Error.NotFound("Offer.NotFound", "La oferta especificada no existe."));
        }

        if (offer.MerchantId != comercio.Id)
        {
            return Result.Failure<OfferDto>(Error.Forbidden("Offer.Forbidden", "No tiene autorización para republicar una oferta de otro comercio."));
        }

        DateTime now = _dateTimeProvider.UtcNow;
        OfferStatus previousStatus = offer.Status;
        Result republishResult = offer.Republish(request.NewStartsAt, request.NewEndsAt, request.NewStock, now);
        if (republishResult.IsFailure)
        {
            return Result.Failure<OfferDto>(republishResult.Error);
        }

        int maxActive = _limitSettings?.Value.MaxActivePerMerchant ?? OfferLimitSettings.DefaultMaxActivePerMerchant;
        long activeCount = await _offerRepository.CountActiveByMerchantAsync(comercio.Id, now, cancellationToken);
        if (activeCount >= maxActive)
        {
            return Result.Failure<OfferDto>(Error.Conflict(
                "Offer.ActiveLimitReached",
                $"El comercio alcanzó el límite máximo de {maxActive} ofertas activas simultáneas."));
        }

        ModerationEvaluation evaluation = await ContentModerationHelper.EvaluateAsync(
            _textModerationService,
            $"Título: {offer.Title}\nDescripción: {offer.Description}\nCategoría: {offer.Category ?? string.Empty}",
            "Republicación de producto u oferta comercial",
            "Offer.ContentModerationRejected",
            "El título o descripción de la publicación contiene lenguaje antisonante o prohibido.",
            cancellationToken);

        // Spec 021 (FR-021-01): la republicación también deja evidencia de la decisión.
        ModerationAuditEntry auditEntry = new(
            offer.Id,
            ModerationTargetType.Offer,
            evaluation.Verdict,
            evaluation.Decision,
            "offer_republish",
            evaluation.FlaggedCategories,
            evaluation.CategoryScores);

        if (!evaluation.IsApproved)
        {
            await _auditRecorder.RecordBestEffortAsync(auditEntry, cancellationToken);

            return Result.Failure<OfferDto>(
                evaluation.RejectionError
                ?? Error.Validation("Offer.ContentModerationRejected", "El título o descripción de la publicación contiene lenguaje antisonante o prohibido."));
        }

        // Spec 021 (FR-021-01): sin evidencia persistida la oferta no se republica (fail-closed).
        Result auditResult = await _auditRecorder.RecordAsync(auditEntry, cancellationToken);
        if (auditResult.IsFailure)
        {
            return Result.Failure<OfferDto>(auditResult.Error);
        }

        await _offerRepository.UpdateAsync(offer, cancellationToken);
        await _cacheService.InvalidateAsync(offer.Id, cancellationToken);

        if (_statusHistoryRepository is not null)
        {
            await _statusHistoryRepository.AddAsync(
                new OfferStatusTransition(offer.Id, previousStatus, offer.Status, userId, "Republish", now),
                cancellationToken);
        }

        _logger.LogInformation("Oferta {OfferId} republicada exitosamente por el comercio {ComercioId}. Nueva vigencia hasta {EndsAtUtc}",
            offer.Id, comercio.Id, offer.EndsAt);

        return Result.Success(offer.ToDto());
    }
}
