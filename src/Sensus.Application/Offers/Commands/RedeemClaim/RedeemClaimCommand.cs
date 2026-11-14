using MediatR;
using Sensus.Application.Offers.DTOs;
using Sensus.Domain.Common;

namespace Sensus.Application.Offers.Commands.RedeemClaim;

/// <summary>
/// FR-033-06 a FR-033-08: canjea un código de apartado por el comerciante propietario.
/// </summary>
public sealed record RedeemClaimCommand(string Code) : IRequest<Result<RedeemClaimResultDto>>;
