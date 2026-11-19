using MediatR;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Offers.DTOs;
using Sensus.Domain.Common;
using Sensus.Domain.Enums;
using Sensus.Domain.Repositories;

namespace Sensus.Application.Offers.Queries.GetOfferClaim;

public sealed record GetOfferClaimQuery(Guid ClaimId) : IRequest<Result<OfferClaimDetailDto>>;

public sealed class GetOfferClaimQueryHandler(
    IOfferClaimRepository claims,
    IComercioRepository merchants,
    ICurrentUserService currentUser,
    IDateTimeProvider clock,
    IClaimCodeService codes) : IRequestHandler<GetOfferClaimQuery, Result<OfferClaimDetailDto>>
{
    public async Task<Result<OfferClaimDetailDto>> Handle(GetOfferClaimQuery request, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(currentUser.UserId, out Guid userId))
        {
            return Result.Failure<OfferClaimDetailDto>(Error.Unauthorized("OfferClaim.Unauthorized", "Debe iniciar sesión."));
        }

        var claim = await claims.GetByIdAsync(request.ClaimId, cancellationToken);
        var notFound = Error.NotFound("OfferClaim.NotFound", "No se encontró el apartado.");
        if (claim is null)
        {
            return Result.Failure<OfferClaimDetailDto>(notFound);
        }

        bool buyer = claim.UserId == userId;
        if (!buyer)
        {
            var merchant = await merchants.GetByIdAsync(claim.MerchantId, cancellationToken);
            if (merchant is not { Activo: true } || merchant.UsuarioId != userId)
            {
                return Result.Failure<OfferClaimDetailDto>(notFound);
            }
        }

        DateTime now = clock.UtcNow;
        bool active = claim.IsActive(now);
        string? qrPayload = null;
        if (buyer && active)
        {
            ClaimCode code = codes.Generate(claim.Id, claim.OfferId, claim.ExpiresAtUtc);
            if (string.Equals(code.Hash, claim.CodeHash, StringComparison.Ordinal) && codes.TryValidate(code.Code, now, out _))
            {
                qrPayload = code.QrPayload;
            }
        }

        var status = !active && claim.Status == OfferClaimStatus.Active ? OfferClaimStatus.Expired : claim.Status;
        return Result.Success(new OfferClaimDetailDto(claim.Id, claim.OfferId, status.ToString(), claim.ExpiresAtUtc, qrPayload));
    }
}
