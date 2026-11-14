using FluentValidation;
using Sensus.Domain.Enums;
using Sensus.Domain.Policies;

namespace Sensus.Application.Offers.Queries.GetNearbyOffers;

/// <summary>
/// Validación de borde de la consulta de ofertas cercanas. El rango de producto según el rol se
/// resuelve en el handler con <see cref="NearbyOffersRadiusPolicy"/> (FR-007-06, FR-007-08 y FR-007-09).
/// </summary>
public sealed class GetNearbyOffersQueryValidator : AbstractValidator<GetNearbyOffersQuery>
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;

    public GetNearbyOffersQueryValidator()
    {
        RuleFor(x => x.Latitude)
            .InclusiveBetween(-90d, 90d)
            .WithMessage("La latitud debe estar entre -90 y 90 grados.");

        RuleFor(x => x.Longitude)
            .InclusiveBetween(-180d, 180d)
            .WithMessage("La longitud debe estar entre -180 y 180 grados.");

        RuleFor(x => x.RadiusKm)
            .Must(radius => !radius.HasValue ||
                            (!double.IsNaN(radius.Value) &&
                             radius.Value > 0 &&
                             radius.Value <= NearbyOffersRadiusPolicy.TechnicalMaxRadiusKm))
            .WithMessage($"El radio debe ser mayor a 0 y no puede superar {NearbyOffersRadiusPolicy.TechnicalMaxRadiusKm} km.");

        RuleFor(x => x.Category)
            .Must(category => string.IsNullOrWhiteSpace(category) || MerchantCategoryCatalog.TryParse(category, out _))
            .WithMessage($"La categoría debe pertenecer al catálogo: {string.Join(", ", MerchantCategoryCatalog.Names)}.");

        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1)
            .WithMessage("La página debe ser mayor o igual a 1.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, MaxPageSize)
            .WithMessage($"El tamaño de página debe estar entre 1 y {MaxPageSize}.");
    }
}
