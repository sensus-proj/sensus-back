using FluentValidation;

namespace Sensus.Application.Offers.Commands.RepublishOffer;

public sealed class RepublishOfferCommandValidator : AbstractValidator<RepublishOfferCommand>
{
    public RepublishOfferCommandValidator()
    {
        RuleFor(x => x.OfferId)
            .NotEmpty().WithMessage("El identificador de la oferta es obligatorio.");

        RuleFor(x => x.NewStock)
            .GreaterThanOrEqualTo(1).WithMessage("El nuevo stock debe ser de al menos 1 unidad.");

        RuleFor(x => x.NewEndsAt)
            .GreaterThan(x => x.NewStartsAt).WithMessage("La nueva fecha de fin debe ser posterior a la fecha de inicio.");
    }
}
