namespace Sensus.Infrastructure.Services;

/// <summary>
/// Configuración de la firma HMAC de los códigos de apartado (spec 033). El secreto se resuelve
/// por entorno o AWS Secrets Manager y nunca se versiona.
/// </summary>
public sealed class ClaimCodeSettings
{
    public const string SectionName = "ClaimCodeSettings";

    public string Secret { get; set; } = string.Empty;
}
