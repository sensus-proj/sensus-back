using MediatR;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Offers.DTOs;
using Sensus.Application.Offers.Notifications;
using Sensus.Domain.Common;
using Sensus.Domain.Entities;
using Sensus.Domain.Enums;
using Sensus.Domain.Repositories;

namespace Sensus.Application.Offers.Commands.ClaimOffer;

public sealed class ClaimOfferCommandHandler : IRequestHandler<ClaimOfferCommand, Result<ClaimOfferResultDto>>
{
    private readonly IOfferRepository _offerRepository;
    private readonly IOfferClaimRepository _offerClaimRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IClaimCodeService _claimCodeService;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ICacheService? _cacheService;
    private readonly IClaimPushNotifier? _claimPushNotifier;
    private readonly IOfferInteractionRecorder? _interactionRecorder;

    public ClaimOfferCommandHandler(
        IOfferRepository offerRepository,
        IOfferClaimRepository offerClaimRepository,
        ICurrentUserService currentUserService,
        IClaimCodeService claimCodeService,
        IDateTimeProvider dateTimeProvider,
        ICacheService? cacheService = null,
        IClaimPushNotifier? claimPushNotifier = null,
        IOfferInteractionRecorder? interactionRecorder = null)
    {
        _offerRepository = offerRepository;
        _offerClaimRepository = offerClaimRepository;
        _currentUserService = currentUserService;
        _claimCodeService = claimCodeService;
        _dateTimeProvider = dateTimeProvider;
        _cacheService = cacheService;
        _claimPushNotifier = claimPushNotifier;
        _interactionRecorder = interactionRecorder;
    }

    public Task<Result<ClaimOfferResultDto>> Handle(
        ClaimOfferCommand request,
        CancellationToken cancellationToken) =>
        OfferClaimFlow.ExecuteAsync(
            [new CartClaimItemInput(request.OfferId, request.Quantity)],
            _offerRepository,
            _offerClaimRepository,
            _currentUserService,
            _claimCodeService,
            _dateTimeProvider,
            _cacheService,
            _claimPushNotifier,
            _interactionRecorder,
            null,
            cancellationToken);
}
