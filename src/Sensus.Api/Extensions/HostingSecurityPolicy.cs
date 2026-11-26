namespace Sensus.Api.Extensions;

/// <summary>
/// Decisiones del pipeline HTTP que dependen del entorno y de si la API corre en AWS Lambda
/// (FR-011-05, brecha G-15). Se aíslan aquí para probarlas sin depender de variables de entorno del proceso.
/// </summary>
public static class HostingSecurityPolicy
{
    /// <summary>
    /// Swagger UI y el documento OpenAPI solo se exponen en Development: en producción revelarían la superficie
    /// completa de la API (OWASP API9, inventario).
    /// </summary>
    public static bool ExposeApiDocs(IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(environment);
        return environment.IsDevelopment();
    }

    /// <summary>El sembrado del Administrador sigue disponible en Development y en Lambda.</summary>
    public static bool SeedAdministrator(IHostEnvironment environment, bool isLambdaRuntime)
    {
        ArgumentNullException.ThrowIfNull(environment);
        return environment.IsDevelopment() || isLambdaRuntime;
    }

    /// <summary>HSTS en todo entorno distinto de Development, incluida Lambda.</summary>
    public static bool UseHsts(IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(environment);
        return !environment.IsDevelopment();
    }

    /// <summary>
    /// Redirección a HTTPS fuera de Development. En Lambda no aplica: API Gateway solo acepta HTTPS y una
    /// redirección basada en el esquema interno podría generar bucles.
    /// </summary>
    public static bool UseHttpsRedirection(IHostEnvironment environment, bool isLambdaRuntime)
    {
        ArgumentNullException.ThrowIfNull(environment);
        return !environment.IsDevelopment() && !isLambdaRuntime;
    }
}
