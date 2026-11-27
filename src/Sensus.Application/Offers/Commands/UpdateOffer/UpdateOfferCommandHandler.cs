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

namespace Sensus.Application.Offers.Commands.UpdateOffer;

public sealed class UpdateOfferCommandHandler : IRequestHandler<UpdateOfferCommand, Result<OfferDto>>
{
    private readonly IOfferRepository _offerRepository;
    private readonly IOfferClaimRepository _offerClaimRepository;
    private readonly IComercioRepository _comercioRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ILogger<UpdateOfferCommandHandler> _logger;
    private readonly ICacheService? _cacheService;
    private readonly IOptions<OfferCategorySettings>? _categorySettings;
    private readonly ITextModerationService? _textModerationService;
    private readonly IModerationAuditRecorder _auditRecorder;

    public UpdateOfferCommandHandler(
        IOfferRepository offerRepository,
        IOfferClaimRepository offerClaimRepository,
        IComercioRepository comercioRepository,
        ICurrentUserService currentUserService,
        IDateTimeProvider dateTimeProvider,
        ILogger<UpdateOfferCommandHandler> logger,
        IModerationAuditRecorder auditRecorder,
        ICacheService? cacheService = null,
        IOptions<OfferCategorySettings>? categorySettings = null,
        ITextModerationService? textModerationService = null)
    {
        _offerRepository = offerRepository;
        _offerClaimRepository = offerClaimRepository;
        _comercioRepository = comercioRepository;
        _currentUserService = currentUserService;
        _dateTimeProvider = dateTimeProvider;
        _logger = logger;
        _auditRecorder = auditRecorder;
        _cacheService = cacheService;
        _categorySettings = categorySettings;
        _textModerationService = textModerationService;
    }

    public async Task<Result<OfferDto>> Handle(UpdateOfferCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_currentUserService.UserId) || !Guid.TryParse(_currentUserService.UserId, out var userId))
        {
            return Result.Failure<OfferDto>(Error.Unauthorized("Offer.Unauthorized", "Debe iniciar sesión para editar ofertas."));
        }

        Comercio? comercio = await _comercioRepository.GetByUsuarioIdAsync(userId, cancellationToken);
        if (comercio is null)
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
            return Result.Failure<OfferDto>(Error.Forbidden("Offer.Forbidden", "No tiene autorización para editar una oferta de otro comercio."));
        }

        int activeClaimsCount = await _offerClaimRepository.GetActiveClaimCountAsync(offer.Id, cancellationToken);

        DateTime now = _dateTimeProvider.UtcNow;
        bool requireStrictCategory = _categorySettings?.Value.RequireStrictValidation ?? false;
        Result<bool> updateResult = offer.Update(
            request.Title,
            request.Description,
            request.OriginalPrice,
            request.DiscountPercentage,
            request.Stock,
            request.StartsAt,
            request.EndsAt,
            request.ImageUrl,
            activeClaimsCount,
            request.ExpectedVersion,
            now,
            request.Category,
            requireStrictCategory);

        if (updateResult.IsFailure)
        {
            return Result.Failure<OfferDto>(updateResult.Error);
        }

        bool contentChanged = updateResult.Value;
        if (contentChanged)
        {
            _logger.LogInformation(
                "Contenido visible de la oferta {OfferId} modificado por el comercio {ComercioId}. Ejecutando re-moderación.",
                offer.Id, comercio.Id);

            ModerationEvaluation evaluation = await ContentModerationHelper.EvaluateAsync(
                _textModerationService,
                $"Título: {request.Title}\nDescripción: {request.Description}\nCategoría: {request.Category ?? string.Empty}",
                "Edición de publicación de producto u oferta comercial",
                "Offer.ContentModerationRejected",
                "El título o descripción de la publicación contiene lenguaje antisonante o prohibido.",
                cancellationToken);

            // Spec 021 (FR-021-01): la re-moderación de una edición también queda auditada.
            ModerationAuditEntry auditEntry = new(
                offer.Id,
                ModerationTargetType.Offer,
                evaluation.Verdict,
                evaluation.Decision,
                "offer_update",
                evaluation.FlaggedCategories,
                evaluation.CategoryScores);

            if (!evaluation.IsApproved)
            {
                await _auditRecorder.RecordBestEffortAsync(auditEntry, cancellationToken);

                return Result.Failure<OfferDto>(
                    evaluation.RejectionError
                    ?? Error.Validation("Offer.ContentModerationRejected", "El título o descripción de la publicación contiene lenguaje antisonante o prohibido."));
            }

            // Spec 021 (FR-021-01): sin evidencia persistida la edición no se publica (fail-closed).
            Result auditResult = await _auditRecorder.RecordAsync(auditEntry, cancellationToken);
            if (auditResult.IsFailure)
            {
                return Result.Failure<OfferDto>(auditResult.Error);
            }
        }

        bool updated = await _offerRepository.UpdateWithVersionCheckAsync(offer, request.ExpectedVersion, cancellationToken);
        if (!updated)
        {
            return Result.Failure<OfferDto>(Error.Conflict("Offer.VersionConflict", "La oferta fue modificada concurrentemente por otra solicitud."));
        }

        await _cacheService.InvalidateAsync(offer.Id, cancellationToken);

        _logger.LogInformation("Oferta {OfferId} actualizada exitosamente a la versión {Version} por el comercio {ComercioId}.",
            offer.Id, offer.Version, comercio.Id);

        return Result.Success(offer.ToDto());
    }
}
