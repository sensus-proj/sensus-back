using FluentValidation;

namespace Sensus.Application.Offers.Queries.GetOfferFeed;

/// <summary>
/// Validación de borde con lista de permitidos para <see cref="GetOfferFeedQuery"/> (FR-042-13, Principio VIII).
/// </summary>
public sealed class GetOfferFeedQueryValidator : AbstractValidator<GetOfferFeedQuery>
{
    public const int MinRadiusKm = 1;
    public const int MaxRadiusKm = 50;
    public const int MaxPageSize = 100;

    public GetOfferFeedQueryValidator()
    {
        RuleFor(x => x.Lat)
            .Must(v => !double.IsNaN(v) && !double.IsInfinity(v))
            .WithMessage("La latitud debe ser un número finito.")
            .InclusiveBetween(-90d, 90d)
            .WithMessage("La latitud debe estar entre -90 y 90.");

        RuleFor(x => x.Lng)
            .Must(v => !double.IsNaN(v) && !double.IsInfinity(v))
            .WithMessage("La longitud debe ser un número finito.")
            .InclusiveBetween(-180d, 180d)
            .WithMessage("La longitud debe estar entre -180 y 180.");

        RuleFor(x => x.RadiusKm)
            .InclusiveBetween(MinRadiusKm, MaxRadiusKm)
            .WithMessage("El radio debe estar entre 1 y 50 km.");

        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1)
            .WithMessage("La página debe ser mayor o igual a 1.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, MaxPageSize)
            .WithMessage("El tamaño de página debe estar entre 1 y 100.");
    }
}
