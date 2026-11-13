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

    public ExpireOffersCommandHandler(
        IOfferRepository offerRepository,
        IDateTimeProvider dateTimeProvider,
        ILogger<ExpireOffersCommandHandler> logger)
    {
        _offerRepository = offerRepository;
        _dateTimeProvider = dateTimeProvider;
        _logger = logger;
    }

    public async Task<Result<ExpireOffersResultDto>> Handle(ExpireOffersCommand request, CancellationToken cancellationToken)
    {
        DateTime now = _dateTimeProvider.UtcNow;
        _logger.LogInformation("Iniciando verificación automática de expiración de ofertas a las {NowUtc}", now);

        var expiredCount = await _offerRepository.ExpireOffersAsync(now, cancellationToken);

        _logger.LogInformation("Proceso de vigencia finalizado. Se marcaron {Count} ofertas como Expired.", expiredCount);

        var result = new ExpireOffersResultDto(expiredCount, now);
        return Result.Success(result);
    }
}
