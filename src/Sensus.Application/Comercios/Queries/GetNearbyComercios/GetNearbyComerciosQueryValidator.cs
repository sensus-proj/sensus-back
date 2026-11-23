using FluentValidation;

namespace Sensus.Application.Comercios.Queries.GetNearbyComercios;

public sealed class GetNearbyComerciosQueryValidator : AbstractValidator<GetNearbyComerciosQuery>
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;

    public GetNearbyComerciosQueryValidator()
    {
        RuleFor(query => query.Latitud)
            .Must(latitude => latitude is >= -90 and <= 90)
            .WithMessage("La latitud debe estar entre -90 y 90 grados.");
        RuleFor(query => query.Longitud)
            .Must(longitude => longitude is >= -180 and <= 180)
            .WithMessage("La longitud debe estar entre -180 y 180 grados.");
        RuleFor(query => query.RadiusKm)
            .Must(radius => radius is null || (double.IsFinite(radius.Value) && radius.Value > 0))
            .WithMessage("El radio en kilómetros debe ser un número positivo.");
        RuleFor(query => query.Page).GreaterThanOrEqualTo(1);
        RuleFor(query => query.PageSize).InclusiveBetween(1, MaxPageSize);
    }
}
