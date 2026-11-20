using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Common.Moderation;
using Sensus.Application.Common.Settings;
using Sensus.Application.Offers.DTOs;
using Sensus.Application.Offers.Notifications;
using Sensus.Domain.Common;
using Sensus.Domain.Entities;
using Sensus.Domain.Repositories;

namespace Sensus.Application.Offers.Commands.CreateOffer;

public sealed class CreateOfferCommandHandler : IRequestHandler<CreateOfferCommand, Result<OfferDto>>
{
    private readonly IOfferRepository _offerRepository;
    private readonly IComercioRepository _comercioRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ILogger<CreateOfferCommandHandler> _logger;
    private readonly ICacheService? _cacheService;
    private readonly INearbyOfferNotifier? _nearbyOfferNotifier;
    private readonly IOptions<OfferCategorySettings>? _categorySettings;
    private readonly ITextModerationService? _textModerationService;

    public CreateOfferCommandHandler(
        IOfferRepository offerRepository,
        IComercioRepository comercioRepository,
        ICurrentUserService currentUserService,
        IDateTimeProvider dateTimeProvider,
        ILogger<CreateOfferCommandHandler> logger,
        ICacheService? cacheService = null,
        INearbyOfferNotifier? nearbyOfferNotifier = null,
        IOptions<OfferCategorySettings>? categorySettings = null,
        ITextModerationService? textModerationService = null)
    {
        _offerRepository = offerRepository;
        _comercioRepository = comercioRepository;
        _currentUserService = currentUserService;
        _dateTimeProvider = dateTimeProvider;
        _logger = logger;
        _cacheService = cacheService;
        _nearbyOfferNotifier = nearbyOfferNotifier;
        _categorySettings = categorySettings;
        _textModerationService = textModerationService;
    }

    public async Task<Result<OfferDto>> Handle(CreateOfferCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_currentUserService.UserId) || !Guid.TryParse(_currentUserService.UserId, out var userId))
        {
            return Result.Failure<OfferDto>(Error.Unauthorized("Offer.Unauthorized", "Debe iniciar sesión para publicar ofertas."));
        }

        Comercio? comercio = await _comercioRepository.GetByUsuarioIdAsync(userId, cancellationToken);
        if (comercio is null || !comercio.Activo)
        {
            return Result.Failure<OfferDto>(Error.Forbidden("Offer.NoActiveMerchant", "El usuario no tiene un comercio activo registrado para publicar ofertas."));
        }

        bool requireStrictCategory = _categorySettings?.Value.RequireStrictValidation ?? false;
        var moderationResult = await ContentModerationHelper.EnsureApprovedAsync(
            _textModerationService,
            $"Título: {request.Title}\nDescripción: {request.Description}\nCategoría: {request.Category ?? string.Empty}",
            "Publicación de producto u oferta comercial",
            "Offer.ContentModerationRejected",
            "El título o descripción de la publicación contiene lenguaje antisonante o prohibido.",
            cancellationToken);

        if (moderationResult.IsFailure)
        {
            return Result.Failure<OfferDto>(moderationResult.Error);
        }

        Result<Offer> offerResult = Offer.Create(
            comercio.Id,
            request.Title,
            request.Description,
            request.OriginalPrice,
            request.DiscountPercentage,
            request.Stock,
            request.StartsAt,
            request.EndsAt,
            request.ImageUrl,
            comercio.Latitud,
            comercio.Longitud,
            _dateTimeProvider.UtcNow,
            request.Category,
            requireStrictCategory);

        if (offerResult.IsFailure)
        {
            return Result.Failure<OfferDto>(offerResult.Error);
        }

        Offer offer = offerResult.Value;
        await _offerRepository.AddAsync(offer, cancellationToken);
        await _cacheService.InvalidateAsync(offer.Id, cancellationToken);

        _logger.LogInformation("Oferta {OfferId} creada exitosamente para el comercio {ComercioId}. Vigencia hasta {EndsAtUtc}",
            offer.Id, comercio.Id, offer.EndsAt);

        // FR-044-08: el aviso se intenta después de persistir y nunca revierte la creación.
        if (_nearbyOfferNotifier is not null)
        {
            await _nearbyOfferNotifier.NotifyAsync(offer, userId, cancellationToken);
        }

        return Result.Success(offer.ToDto());
    }
}
