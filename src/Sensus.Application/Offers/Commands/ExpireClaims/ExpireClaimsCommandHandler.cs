using MediatR;
using Microsoft.Extensions.Logging;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Offers.DTOs;
using Sensus.Domain.Common;
using Sensus.Domain.Entities;
using Sensus.Domain.Repositories;

namespace Sensus.Application.Offers.Commands.ExpireClaims;

public sealed class ExpireClaimsCommandHandler : IRequestHandler<ExpireClaimsCommand, Result<ExpireClaimsResultDto>>
{
    private readonly IOfferClaimRepository _offerClaimRepository;
    private readonly IOfferRepository _offerRepository;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ILogger<ExpireClaimsCommandHandler> _logger;

    public ExpireClaimsCommandHandler(
        IOfferClaimRepository offerClaimRepository,
        IOfferRepository offerRepository,
        IDateTimeProvider dateTimeProvider,
        ILogger<ExpireClaimsCommandHandler> logger)
    {
        _offerClaimRepository = offerClaimRepository;
        _offerRepository = offerRepository;
        _dateTimeProvider = dateTimeProvider;
        _logger = logger;
    }

    public async Task<Result<ExpireClaimsResultDto>> Handle(
        ExpireClaimsCommand request,
        CancellationToken cancellationToken)
    {
        DateTime now = _dateTimeProvider.UtcNow;

        IReadOnlyList<OfferClaim> expiredClaims = await _offerClaimRepository.ExpireClaimsAsync(
            now,
            cancellationToken);

        int returnedUnits = 0;
        foreach (IGrouping<Guid, OfferClaim> group in expiredClaims.GroupBy(c => c.OfferId))
        {
            int units = group.Count();
            bool returned = await _offerRepository.ReturnStockAsync(group.Key, units, cancellationToken);
            if (returned)
            {
                returnedUnits += units;
            }
            else
            {
                _logger.LogWarning(
                    "No se pudo devolver stock de la oferta {OfferId} para {Units} apartados expirados.",
                    group.Key,
                    units);
            }
        }

        _logger.LogInformation(
            "Expiración de apartados: {ExpiredCount} apartados vencidos, {ReturnedUnits} unidades devueltas a las {NowUtc}.",
            expiredClaims.Count,
            returnedUnits,
            now);

        return Result.Success(new ExpireClaimsResultDto(expiredClaims.Count, returnedUnits, now));
    }
}
