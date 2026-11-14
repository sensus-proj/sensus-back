using MediatR;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Offers.DTOs;
using Sensus.Domain.Common;
using Sensus.Domain.Entities;
using Sensus.Domain.Repositories;

namespace Sensus.Application.Offers.Commands.RedeemClaim;

public sealed class RedeemClaimCommandHandler : IRequestHandler<RedeemClaimCommand, Result<RedeemClaimResultDto>>
{
    private readonly IOfferClaimRepository _offerClaimRepository;
    private readonly IOfferRepository _offerRepository;
    private readonly IComercioRepository _comercioRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IClaimCodeService _claimCodeService;
    private readonly IDateTimeProvider _dateTimeProvider;

    public RedeemClaimCommandHandler(
        IOfferClaimRepository offerClaimRepository,
        IOfferRepository offerRepository,
        IComercioRepository comercioRepository,
        ICurrentUserService currentUserService,
        IClaimCodeService claimCodeService,
        IDateTimeProvider dateTimeProvider)
    {
        _offerClaimRepository = offerClaimRepository;
        _offerRepository = offerRepository;
        _comercioRepository = comercioRepository;
        _currentUserService = currentUserService;
        _claimCodeService = claimCodeService;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<Result<RedeemClaimResultDto>> Handle(
        RedeemClaimCommand request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_currentUserService.UserId) ||
            !Guid.TryParse(_currentUserService.UserId, out Guid userId))
        {
            return Result.Failure<RedeemClaimResultDto>(
                Error.Unauthorized("OfferClaim.Unauthorized", "Debe iniciar sesión como comerciante para canjear."));
        }

        Comercio? comercio = await _comercioRepository.GetByUsuarioIdAsync(userId, cancellationToken);
        if (comercio is null || !comercio.Activo)
        {
            return Result.Failure<RedeemClaimResultDto>(
                Error.Forbidden("OfferClaim.NoActiveMerchant", "El usuario no tiene un comercio activo."));
        }

        DateTime now = _dateTimeProvider.UtcNow;

        if (!_claimCodeService.TryValidate(request.Code, now, out ClaimCodePayload payload))
        {
            return Result.Failure<RedeemClaimResultDto>(
                Error.Validation("OfferClaim.CodeInvalid", "El código no es válido o ya expiró."));
        }

        string codeHash = _claimCodeService.ComputeHash(request.Code);
        OfferClaim? claim = await _offerClaimRepository.GetByCodeHashAsync(codeHash, cancellationToken);
        if (claim is null || claim.OfferId != payload.OfferId)
        {
            return Result.Failure<RedeemClaimResultDto>(
                Error.NotFound("OfferClaim.NotFound", "No se encontró el apartado asociado al código."));
        }

        Result redeemResult = claim.Redeem(comercio.Id, now);
        if (redeemResult.IsFailure)
        {
            bool stockReleased = await ReleaseStockIfJustExpiredAsync(claim, redeemResult, cancellationToken);
            if (stockReleased)
            {
                await _offerClaimRepository.UpdateAsync(claim, cancellationToken);
            }

            return Result.Failure<RedeemClaimResultDto>(redeemResult.Error);
        }

        bool redeemed = await _offerClaimRepository.TryRedeemAsync(claim.Id, comercio.Id, now, cancellationToken);
        if (!redeemed)
        {
            return Result.Failure<RedeemClaimResultDto>(
                Error.Conflict("OfferClaim.AlreadyRedeemed", "El apartado ya fue canjeado o expiró."));
        }

        return Result.Success(new RedeemClaimResultDto(
            claim.Id,
            claim.OfferId,
            claim.MerchantId,
            claim.RedeemedAtUtc ?? now));
    }

    private async Task<bool> ReleaseStockIfJustExpiredAsync(
        OfferClaim claim,
        Result redeemResult,
        CancellationToken cancellationToken)
    {
        if (redeemResult.Error.Code != "OfferClaim.Expired")
        {
            return false;
        }

        await _offerRepository.ReturnStockAsync(claim.OfferId, 1, cancellationToken);
        return true;
    }
}
