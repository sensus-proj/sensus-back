namespace Sensus.Infrastructure.Authentication;

public sealed class JwtSettings
{
    public const string SectionName = "JwtSettings";

    public string Secret { get; set; } = string.Empty;
    public string Issuer { get; set; } = "SensusApi";
    public string Audience { get; set; } = "SensusApp";
    public int ExpiryMinutes { get; set; } = 15; // Máximo 15 minutos (G-06 / Spec 002)
    public int GuestExpiryMinutes { get; set; } = 15; // 15 minutos máximo para invitados (Spec 052)
    public int RefreshTokenDays { get; set; } = 30;
}
