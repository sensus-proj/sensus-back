using MediatR;
using Sensus.Domain.Common;

namespace Sensus.Application.Offers.Commands.RegisterOfferView;

/// <summary>
/// FR-032-03, FR-032-04 y FR-032-06: registra una vista deduplicada por usuario, oferta y día.
/// </summary>
public sealed record RegisterOfferViewCommand(Guid OfferId) : IRequest<Result>;
