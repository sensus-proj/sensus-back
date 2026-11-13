using FluentValidation;

namespace Sensus.Application.Offers.Commands.CreateOffer;

public sealed class CreateOfferCommandValidator : AbstractValidator<CreateOfferCommand>
{
    public CreateOfferCommandValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("El título de la oferta es obligatorio.")
            .MinimumLength(3).WithMessage("El título debe tener al menos 3 caracteres.")
            .MaximumLength(100).WithMessage("El título no puede exceder los 100 caracteres.");

        RuleFor(x => x.Description)
            .NotEmpty().WithMessage("La descripción de la oferta es obligatoria.")
            .MinimumLength(5).WithMessage("La descripción debe tener al menos 5 caracteres.")
            .MaximumLength(500).WithMessage("La descripción no puede exceder los 500 caracteres.");

        RuleFor(x => x.OriginalPrice)
            .GreaterThan(0).WithMessage("El precio original debe ser mayor a 0.");

        RuleFor(x => x.DiscountPercentage)
            .InclusiveBetween(1, 90).WithMessage("El porcentaje de descuento debe estar entre 1% y 90%.");

        RuleFor(x => x.Stock)
            .GreaterThanOrEqualTo(1).WithMessage("El stock inicial debe ser de al menos 1 unidad.");

        RuleFor(x => x.EndsAt)
            .GreaterThan(x => x.StartsAt).WithMessage("La fecha de fin debe ser posterior a la fecha de inicio.");
    }
}
