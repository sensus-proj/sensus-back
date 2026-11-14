using Sensus.Domain.Common;

namespace Sensus.Domain.Policies;

/// <summary>
/// Política de dominio para resolver el radio efectivo de la búsqueda de ofertas cercanas
/// (FR-007-06, FR-007-08 y FR-007-09).
/// </summary>
public static class NearbyOffersRadiusPolicy
{
    /// <summary>Radio máximo estricto para una sesión de invitado (FR-007-09).</summary>
    public const int GuestMaxRadiusKm = 2;

    /// <summary>Radio mínimo admitido para un usuario registrado (FR-007-08).</summary>
    public const int RegisteredMinRadiusKm = 1;

    /// <summary>Radio máximo admitido para un usuario registrado (FR-007-08).</summary>
    public const int RegisteredMaxRadiusKm = 5;

    /// <summary>Tope técnico de rechazo, independiente del rol (FR-007-06).</summary>
    public const int TechnicalMaxRadiusKm = 50;

    /// <summary>Radio aplicado cuando el cliente no informa uno.</summary>
    public const int DefaultRadiusKm = 2;

    /// <summary>
    /// Resuelve el radio efectivo en metros aplicando, en orden, el tope técnico, el límite de
    /// invitado y el rango de producto del usuario registrado.
    /// </summary>
    public static Result<int> ResolveRadiusMeters(bool isGuest, double? requestedRadiusKm)
    {
        double requested = requestedRadiusKm ?? DefaultRadiusKm;

        if (double.IsNaN(requested) || double.IsInfinity(requested) || requested <= 0)
        {
            return Result.Failure<int>(
                Error.Validation("Offer.RadiusInvalid", "El radio debe ser mayor a 0 km."));
        }

        if (requested > TechnicalMaxRadiusKm)
        {
            return Result.Failure<int>(
                Error.Validation(
                    "Offer.RadiusTooLarge",
                    $"El radio no puede superar el máximo técnico de {TechnicalMaxRadiusKm} km."));
        }

        if (isGuest)
        {
            double guestRadius = Math.Min(requested, GuestMaxRadiusKm);
            return Result.Success(ToMeters(guestRadius));
        }

        if (requested < RegisteredMinRadiusKm || requested > RegisteredMaxRadiusKm)
        {
            return Result.Failure<int>(
                Error.Validation(
                    "Offer.RadiusOutOfRange",
                    $"El radio para un usuario registrado debe estar entre {RegisteredMinRadiusKm} y {RegisteredMaxRadiusKm} km."));
        }

        return Result.Success(ToMeters(requested));
    }

    private static int ToMeters(double radiusKm) => (int)Math.Round(radiusKm * 1000, MidpointRounding.AwayFromZero);
}
