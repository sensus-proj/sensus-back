using FluentValidation;

namespace Sensus.Application.Offers.Queries.GetUserFavorites;

/// <summary>
/// Validación de borde de la consulta paginada de favoritos (20 por defecto, 100 máximo).
/// </summary>
public sealed class GetUserFavoritesQueryValidator : AbstractValidator<GetUserFavoritesQuery>
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;

    public GetUserFavoritesQueryValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1)
            .WithMessage("La página debe ser mayor o igual a 1.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, MaxPageSize)
            .WithMessage($"El tamaño de página debe estar entre 1 y {MaxPageSize}.");
    }
}
