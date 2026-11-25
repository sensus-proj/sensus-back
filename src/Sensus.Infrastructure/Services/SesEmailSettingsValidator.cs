using Microsoft.Extensions.Options;

namespace Sensus.Infrastructure.Services;

/// <summary>
/// Validación fail-closed de la configuración de SES (Spec 029). Sin remitente verificado no se permite
/// arrancar en modo real; el modo simulado queda restringido a Development/Testing.
/// </summary>
public sealed class SesEmailSettingsValidator : IValidateOptions<SesEmailSettings>
{
    public ValidateOptionsResult Validate(string? name, SesEmailSettings options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.Simulate)
        {
            return ValidateOptionsResult.Success;
        }

        if (string.IsNullOrWhiteSpace(options.From))
        {
            return ValidateOptionsResult.Fail(
                $"{SesEmailSettings.SectionName}:From es obligatorio cuando {nameof(SesEmailSettings.Simulate)} es false.");
        }

        if (!string.IsNullOrWhiteSpace(options.Region))
        {
            try
            {
                _ = options.GetRegionEndpoint();
            }
            catch (ArgumentException ex)
            {
                return ValidateOptionsResult.Fail($"{SesEmailSettings.SectionName}:Region no es válida: {ex.Message}");
            }
        }

        return ValidateOptionsResult.Success;
    }
}
