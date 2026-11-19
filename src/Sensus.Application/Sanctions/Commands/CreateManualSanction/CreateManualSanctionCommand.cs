using FluentValidation;
using MediatR;
using Sensus.Application.Sanctions.Common;
using Sensus.Domain.Common;
using Sensus.Domain.Entities;

namespace Sensus.Application.Sanctions.Commands.CreateManualSanction;

/// <summary>
/// FR-020-07: un administrador aplica una sanción manual. <see cref="DurationHours"/> nulo implica baneo permanente.
/// </summary>
public sealed record CreateManualSanctionCommand(Guid UserId, string Reason, int? DurationHours)
    : IRequest<Result<SanctionDto>>;

public sealed class CreateManualSanctionCommandValidator : AbstractValidator<CreateManualSanctionCommand>
{
    public const int MaxDurationHours = 24 * 365;

    public CreateManualSanctionCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(Sanction.MaxReasonLength);
        RuleFor(x => x.DurationHours)
            .InclusiveBetween(1, MaxDurationHours)
            .When(x => x.DurationHours.HasValue);
    }
}
