using Amazon.Extensions.NETCore.Setup;

namespace Sensus.Api.Extensions;

/// <summary>
/// Carga de secretos desde AWS Systems Manager Parameter Store (Principio VIII, regla 5; brecha G-01).
/// Cada parámetro <c>/sensus/{entorno}/Seccion/Clave</c> se convierte en la clave de configuración
/// <c>Seccion:Clave</c> (por ejemplo, <c>/sensus/prod/JwtSettings/Secret</c> → <c>JwtSettings:Secret</c>).
/// Los <c>SecureString</c> se descifran con la clave administrada <c>aws/ssm</c>.
/// </summary>
public static class SecretsConfigurationExtensions
{
    public const string PrefixVariable = "AWS_SSM_PARAMETER_PREFIX";

    /// <summary>
    /// Agrega Parameter Store como la fuente de mayor prioridad cuando existe <see cref="PrefixVariable"/>
    /// (solo en Lambda). Es obligatoria: si AWS no responde, la API no arranca en lugar de arrancar sin secretos.
    /// </summary>
    /// <returns><c>true</c> si se registró Parameter Store.</returns>
    public static bool AddSensusSecretStore(this IConfigurationBuilder configuration, string? prefix)
    {
        if (string.IsNullOrWhiteSpace(prefix))
        {
            return false;
        }

        if (!prefix.StartsWith('/'))
        {
            throw new InvalidOperationException($"{PrefixVariable} debe empezar con '/'.");
        }

        configuration.AddSystemsManager(source =>
        {
            source.Path = prefix;
            source.Optional = false;
            source.AwsOptions = new AWSOptions();
        });

        return true;
    }
}
