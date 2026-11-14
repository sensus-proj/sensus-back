using FluentValidation;
using Sensus.Application.Common.Interfaces;
using Sensus.Domain.Common;

namespace Sensus.Application.Comercios;

/// <summary>
/// Reglas compartidas de validación y resolución de coordenadas para registro y actualización de comercios (FR-005-06 / FR-031-02).
/// </summary>
internal static class ComercioCoordinateHelper
{
    public static bool IsValidLatitude(double lat) => !double.IsNaN(lat) && !double.IsInfinity(lat) && lat >= -90.0 && lat <= 90.0;

    public static bool IsValidLongitude(double lng) => !double.IsNaN(lng) && !double.IsInfinity(lng) && lng >= -180.0 && lng <= 180.0;

    public static void AddCoordinateValidationRules<T>(
        AbstractValidator<T> validator,
        Func<T, double?> getLat,
        Func<T, double?> getLng)
    {
        validator.RuleFor(x => x)
            .Must(x => getLat(x).HasValue == getLng(x).HasValue)
            .WithMessage("Debe proporcionar tanto la latitud como la longitud cuando especifique coordenadas.");

        validator.When(x => getLat(x).HasValue, () =>
        {
            validator.RuleFor(x => getLat(x)!.Value)
                .Must(IsValidLatitude)
                .WithMessage("La latitud debe estar entre -90 y 90 grados.");
        });

        validator.When(x => getLng(x).HasValue, () =>
        {
            validator.RuleFor(x => getLng(x)!.Value)
                .Must(IsValidLongitude)
                .WithMessage("La longitud debe estar entre -180 y 180 grados.");
        });
    }

    public static async Task<Result<(double Latitude, double Longitude)?>> ResolveAsync(
        double? latitud,
        double? longitud,
        bool shouldGeocodeIfMissing,
        string direccion,
        string codigoPostal,
        IGeocodingService geocodingService,
        CancellationToken cancellationToken)
    {
        if (latitud.HasValue != longitud.HasValue)
        {
            return Result.Failure<(double Latitude, double Longitude)?>(
                Error.Validation("Comercio.CoordenadasIncompletas", "Debe proporcionar tanto la latitud como la longitud."));
        }

        if (latitud.HasValue && longitud.HasValue)
        {
            if (!IsValidLatitude(latitud.Value))
            {
                return Result.Failure<(double Latitude, double Longitude)?>(
                    Error.Validation("Comercio.LatitudInvalida", "La latitud debe encontrarse entre -90 y 90 grados."));
            }

            if (!IsValidLongitude(longitud.Value))
            {
                return Result.Failure<(double Latitude, double Longitude)?>(
                    Error.Validation("Comercio.LongitudInvalida", "La longitud debe encontrarse entre -180 y 180 grados."));
            }

            return Result.Success<(double Latitude, double Longitude)?>((
                Math.Round(latitud.Value, 6, MidpointRounding.AwayFromZero),
                Math.Round(longitud.Value, 6, MidpointRounding.AwayFromZero)));
        }

        if (!shouldGeocodeIfMissing)
        {
            return Result.Success<(double Latitude, double Longitude)?>(null);
        }

        var geocodeResult = await geocodingService.GeocodeAddressAsync(direccion, codigoPostal, cancellationToken);
        if (geocodeResult.IsFailure)
        {
            return Result.Failure<(double Latitude, double Longitude)?>(geocodeResult.Error);
        }

        return Result.Success<(double Latitude, double Longitude)?>((
            Math.Round(geocodeResult.Value.Latitude, 6, MidpointRounding.AwayFromZero),
            Math.Round(geocodeResult.Value.Longitude, 6, MidpointRounding.AwayFromZero)));
    }
}
