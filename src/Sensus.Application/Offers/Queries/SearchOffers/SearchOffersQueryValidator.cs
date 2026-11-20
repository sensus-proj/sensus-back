using FluentValidation;

namespace Sensus.Application.Offers.Queries.SearchOffers;

/// <summary>
/// Validación de borde con lista de permitidos para <see cref="SearchOffersQuery"/> (FR-041-08, Principio VIII).
/// </summary>
public sealed class SearchOffersQueryValidator : AbstractValidator<SearchOffersQuery>
{
    public const int MinQueryLength = 2;
    public const int MaxQueryLength = 80;
    public const double MinRadiusKm = 1d;
    public const double MaxRadiusKm = 50d;
    public const int MaxPageSize = 100;

    public SearchOffersQueryValidator()
    {
        RuleFor(x => x.Query)
            .Must(q => q is null || !q.Any(char.IsControl))
            .WithMessage("La consulta no debe contener caracteres de control.")
            .Must(q =>
            {
                if (q is null)
                {
                    return true;
                }

                string trimmed = q.Trim();
                return trimmed.Length == 0 || trimmed.Length is >= MinQueryLength and <= MaxQueryLength;
            })
            .WithMessage($"La consulta debe estar vacía o tener entre {MinQueryLength} y {MaxQueryLength} caracteres.");

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
            .Must(r => !r.HasValue || (!double.IsNaN(r.Value) && !double.IsInfinity(r.Value) && r.Value >= MinRadiusKm && r.Value <= MaxRadiusKm))
            .WithMessage($"El radio debe estar entre {MinRadiusKm} y {MaxRadiusKm} km.");

        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1)
            .WithMessage("La página debe ser mayor o igual a 1.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, MaxPageSize)
            .WithMessage($"El tamaño de página debe estar entre 1 y {MaxPageSize}.");
    }
}
