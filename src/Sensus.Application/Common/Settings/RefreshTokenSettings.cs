using System.ComponentModel.DataAnnotations;

namespace Sensus.Application.Common.Settings;

/// <summary>
/// Configuración compartida para la vigencia de refresh tokens rotatorios (G-06 / Spec 002).
/// Se vincula a la sección <c>JwtSettings</c>.
/// </summary>
public sealed class RefreshTokenSettings
{
    public const string SectionName = "JwtSettings";
    public const int DefaultRefreshTokenDays = 30;

    [Range(1, 90)]
    public int RefreshTokenDays { get; set; } = DefaultRefreshTokenDays;
}
