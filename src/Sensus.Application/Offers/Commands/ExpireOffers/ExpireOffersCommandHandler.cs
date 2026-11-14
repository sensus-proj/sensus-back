using MediatR;
using Microsoft.Extensions.Logging;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Offers.DTOs;
using Sensus.Domain.Common;
using Sensus.Domain.Repositories;

namespace Sensus.Application.Offers.Commands.ExpireOffers;

public sealed class ExpireOffersCommandHandler : IRequestHandler<ExpireOffersCommand, Result<ExpireOffersResultDto>>
{
    private readonly IOfferRepository _offerRepository;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ILogger<ExpireOffersCommandHandler> _logger;
    private readonly ICacheService? _cacheService;

    public ExpireOffersCommandHandler(
        IOfferRepository offerRepository,
        IDateTimeProvider dateTimeProvider,
        ILogger<ExpireOffersCommandHandler> logger,
        ICacheService? cacheService = null)
    {
        _offerRepository = offerRepository;
        _dateTimeProvider = dateTimeProvider;
        _logger = logger;
        _cacheService = cacheService;
    }

    public async Task<Result<ExpireOffersResultDto>> Handle(ExpireOffersCommand request, CancellationToken cancellationToken)
    {
        DateTime now = _dateTimeProvider.UtcNow;
        _logger.LogInformation("Iniciando verificación automática de expiración de ofertas a las {NowUtc}", now);

        int expiredCount = await _offerRepository.ExpireOffersAsync(now, cancellationToken);
        if (expiredCount > 0)
        {
            await _cacheService.InvalidateAsync(null, cancellationToken);
        }

        _logger.LogInformation("Proceso de vigencia finalizado. Se marcaron {Count} ofertas como Expired.", expiredCount);

        ExpireOffersResultDto result = new(expiredCount, now);
        return Result.Success(result);
    }
}
