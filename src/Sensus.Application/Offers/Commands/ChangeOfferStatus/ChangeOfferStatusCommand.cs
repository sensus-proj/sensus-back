using MediatR;
using Sensus.Application.Offers.DTOs;
using Sensus.Domain.Common;

namespace Sensus.Application.Offers.Commands.ChangeOfferStatus;

/// <summary>
/// Spec 036 (FR-036-01): Comando para cambiar el estado del ciclo de vida de una oferta (Published, Paused, Archived).
/// </summary>
public sealed record ChangeOfferStatusCommand(
    Guid OfferId,
    string NewStatus) : IRequest<Result<OfferDto>>;
