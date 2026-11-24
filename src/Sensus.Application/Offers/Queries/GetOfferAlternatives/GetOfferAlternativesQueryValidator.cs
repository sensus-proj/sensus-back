using FluentValidation;
using Sensus.Domain.Models;

namespace Sensus.Application.Offers.Queries.GetOfferAlternatives;

/// <summary>
/// Validación de borde con lista de permitidos para <see cref="GetOfferAlternativesQuery"/> (FR-043-08).
/// </summary>
public sealed class GetOfferAlternativesQueryValidator : AbstractValidator<GetOfferAlternativesQuery>
{
    public GetOfferAlternativesQueryValidator()
    {
        RuleFor(x => x.OfferId)
            .NotEmpty()
            .WithMessage("El identificador de la oferta es obligatorio.");

        RuleFor(x => x.Limit)
            .InclusiveBetween(1, OfferAlternativesPolicy.MaxLimit)
            .WithMessage($"El límite de alternativas debe estar entre 1 y {OfferAlternativesPolicy.MaxLimit}.");
    }
}
