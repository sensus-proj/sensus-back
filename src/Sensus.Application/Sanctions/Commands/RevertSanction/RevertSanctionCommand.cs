using FluentValidation;
using MediatR;
using Sensus.Application.Sanctions.Common;
using Sensus.Domain.Common;
using Sensus.Domain.Entities;

namespace Sensus.Application.Sanctions.Commands.RevertSanction;

/// <summary>
/// FR-020-02: un administrador revierte una sanción activa con un motivo obligatorio.
/// </summary>
public sealed record RevertSanctionCommand(Guid SanctionId, string Reason) : IRequest<Result<SanctionDto>>;

public sealed class RevertSanctionCommandValidator : AbstractValidator<RevertSanctionCommand>
{
    public RevertSanctionCommandValidator()
    {
        RuleFor(x => x.SanctionId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(Sanction.MaxReasonLength);
    }
}
