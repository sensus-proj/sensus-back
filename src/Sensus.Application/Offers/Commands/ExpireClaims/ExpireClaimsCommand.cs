using MediatR;
using Sensus.Application.Offers.DTOs;
using Sensus.Domain.Common;

namespace Sensus.Application.Offers.Commands.ExpireClaims;

/// <summary>
/// FR-033-05: expira los apartados vencidos y libera el stock reservado.
/// </summary>
public sealed record ExpireClaimsCommand : IRequest<Result<ExpireClaimsResultDto>>;
