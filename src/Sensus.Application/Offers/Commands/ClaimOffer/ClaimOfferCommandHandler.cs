using MediatR;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Offers.DTOs;
using Sensus.Domain.Common;
using Sensus.Domain.Entities;
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

    public ClaimOfferCommandHandler(
        IOfferRepository offerRepository,
        IOfferClaimRepository offerClaimRepository,
        ICurrentUserService currentUserService,
        IClaimCodeService claimCodeService,
        IDateTimeProvider dateTimeProvider,
        ICacheService? cacheService = null)
    {
        _offerRepository = offerRepository;
        _offerClaimRepository = offerClaimRepository;
        _currentUserService = currentUserService;
        _claimCodeService = claimCodeService;
        _dateTimeProvider = dateTimeProvider;
        _cacheService = cacheService;
    }

    public async Task<Result<ClaimOfferResultDto>> Handle(
        ClaimOfferCommand request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_currentUserService.UserId) ||
            !Guid.TryParse(_currentUserService.UserId, out Guid userId))
        {
            return Result.Failure<ClaimOfferResultDto>(
                Error.Unauthorized("OfferClaim.Unauthorized", "Debe iniciar sesión para apartar una oferta."));
        }

        DateTime now = _dateTimeProvider.UtcNow;

        Offer? offer = await _offerRepository.GetByIdAsync(request.OfferId, cancellationToken);
        if (offer is null || !offer.IsActive(now))
        {
            return Result.Failure<ClaimOfferResultDto>(
                Error.Conflict("OfferClaim.OfferNotAvailable", "La oferta no está disponible para apartar."));
        }

        OfferClaim? existingClaim = await _offerClaimRepository.GetActiveByUserAndOfferAsync(
            userId,
            offer.Id,
            cancellationToken);

        if (existingClaim is not null)
        {
            return Result.Failure<ClaimOfferResultDto>(
                Error.Conflict("OfferClaim.AlreadyActive", "Ya tiene un apartado activo para esta oferta."));
        }

        bool reserved = await _offerRepository.TryReserveStockAsync(offer.Id, now, cancellationToken);
        if (!reserved)
        {
            return Result.Failure<ClaimOfferResultDto>(
                Error.Conflict("OfferClaim.NoStock", "La oferta ya no tiene stock disponible."));
        }

        Result<OfferClaim> claimResult = OfferClaim.Create(userId, offer.Id, offer.MerchantId, now);
        if (claimResult.IsFailure)
        {
            await _offerRepository.ReturnStockAsync(offer.Id, 1, cancellationToken);
            return Result.Failure<ClaimOfferResultDto>(claimResult.Error);
        }

        OfferClaim claim = claimResult.Value;
        ClaimCode generatedCode = _claimCodeService.Generate(claim.Id, offer.Id, claim.ExpiresAtUtc);
        Result hashResult = claim.AssignCodeHash(generatedCode.Hash);
        if (hashResult.IsFailure)
        {
            await _offerRepository.ReturnStockAsync(offer.Id, 1, cancellationToken);
            return Result.Failure<ClaimOfferResultDto>(hashResult.Error);
        }

        bool added = await _offerClaimRepository.TryAddAsync(claim, cancellationToken);
        if (!added)
        {
            // Compensación: otro apartado activo ganó la carrera; se devuelve la unidad reservada.
            await _offerRepository.ReturnStockAsync(offer.Id, 1, cancellationToken);
            return Result.Failure<ClaimOfferResultDto>(
                Error.Conflict("OfferClaim.AlreadyActive", "Ya tiene un apartado activo para esta oferta."));
        }

        await _cacheService.InvalidateAsync(offer.Id, cancellationToken);

        return Result.Success(new ClaimOfferResultDto(
            claim.Id,
            claim.OfferId,
            generatedCode.Code,
            generatedCode.QrPayload,
            claim.ExpiresAtUtc));
    }
}
