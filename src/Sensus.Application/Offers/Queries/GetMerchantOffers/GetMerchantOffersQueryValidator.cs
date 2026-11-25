using FluentValidation;
using Sensus.Application.Common.Models;
using Sensus.Domain.Enums;

namespace Sensus.Application.Offers.Queries.GetMerchantOffers;

/// <summary>
/// Validación de borde de la consulta de inventario (Spec 037, FR-037-02 y FR-037-04).
/// Las invariantes de propiedad del comercio se comprueban en el handler.
/// </summary>
public sealed class GetMerchantOffersQueryValidator : AbstractValidator<GetMerchantOffersQuery>
{
    public GetMerchantOffersQueryValidator()
    {
        RuleFor(x => x.StatusFilter)
            .Must(status => string.IsNullOrWhiteSpace(status) ||
                            Enum.TryParse<OfferStatus>(status.Trim(), ignoreCase: true, out _))
            .WithMessage("El estado debe ser Draft, Published, Paused, Expired o Archived.");

        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1)
            .WithMessage("La página debe ser mayor o igual a 1.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, PaginationPolicy.MaxPageSize)
            .WithMessage($"El tamaño de página debe estar entre 1 y {PaginationPolicy.MaxPageSize}.");
    }
}
