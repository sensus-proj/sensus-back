using MediatR;
using Microsoft.Extensions.Logging;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Offers.DTOs;
using Sensus.Application.Offers.Notifications;
using Sensus.Domain.Common;
using Sensus.Domain.Repositories;

namespace Sensus.Application.Offers.Commands.ClaimOffer;

/// <summary>
/// Genera un único apartado (y un único código QR) para múltiples productos y cantidades de un mismo comercio.
/// </summary>
public sealed class CreateCartClaimCommandHandler : IRequestHandler<CreateCartClaimCommand, Result<ClaimOfferResultDto>>
{
    private readonly IOfferRepository _offerRepository;
    private readonly IOfferClaimRepository _offerClaimRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IClaimCodeService _claimCodeService;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ICacheService _cacheService;
    private readonly IClaimPushNotifier? _claimPushNotifier;
    private readonly IOfferInteractionRecorder? _interactionRecorder;
    private readonly ILogger<CreateCartClaimCommandHandler>? _logger;

    public CreateCartClaimCommandHandler(
        IOfferRepository offerRepository,
        IOfferClaimRepository offerClaimRepository,
        ICurrentUserService currentUserService,
        IClaimCodeService claimCodeService,
        IDateTimeProvider dateTimeProvider,
        ICacheService cacheService,
        IClaimPushNotifier? claimPushNotifier = null,
        IOfferInteractionRecorder? interactionRecorder = null,
        ILogger<CreateCartClaimCommandHandler>? logger = null)
    {
        _offerRepository = offerRepository;
        _offerClaimRepository = offerClaimRepository;
        _currentUserService = currentUserService;
        _claimCodeService = claimCodeService;
        _dateTimeProvider = dateTimeProvider;
        _cacheService = cacheService;
        _claimPushNotifier = claimPushNotifier;
        _interactionRecorder = interactionRecorder;
        _logger = logger;
    }

    public Task<Result<ClaimOfferResultDto>> Handle(
        CreateCartClaimCommand request,
        CancellationToken cancellationToken) =>
        OfferClaimFlow.ExecuteAsync(
            request.Items,
            _offerRepository,
            _offerClaimRepository,
            _currentUserService,
            _claimCodeService,
            _dateTimeProvider,
            _cacheService,
            _claimPushNotifier,
            _interactionRecorder,
            _logger,
            cancellationToken);
}
