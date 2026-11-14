using FluentValidation;

namespace Sensus.Application.Offers.Commands.ChangeOfferStatus;

public sealed class ChangeOfferStatusCommandValidator : AbstractValidator<ChangeOfferStatusCommand>
{
    private static readonly string[] AllowedStatuses = ["Published", "Paused", "Archived"];

    public ChangeOfferStatusCommandValidator()
    {
        RuleFor(x => x.OfferId)
            .NotEmpty().WithMessage("El identificador de la oferta es obligatorio.");

        RuleFor(x => x.NewStatus)
            .NotEmpty().WithMessage("El nuevo estado es obligatorio.")
            .Must(status => AllowedStatuses.Contains(status, StringComparer.OrdinalIgnoreCase))
            .WithMessage("El nuevo estado debe ser 'Published', 'Paused' o 'Archived'.");
    }
}
