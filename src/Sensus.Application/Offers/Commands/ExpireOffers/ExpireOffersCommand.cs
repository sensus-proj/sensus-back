using MediatR;
using Sensus.Application.Offers.DTOs;
using Sensus.Domain.Common;

namespace Sensus.Application.Offers.Commands.ExpireOffers;

/// <summary>
/// SE-022 a SE-025: Comando para ejecutar la transición automática de vigencia en MongoDB de ofertas expiradas (Sustituto de Trigger/Job SQL).
/// </summary>
public sealed record ExpireOffersCommand() : IRequest<Result<ExpireOffersResultDto>>;
