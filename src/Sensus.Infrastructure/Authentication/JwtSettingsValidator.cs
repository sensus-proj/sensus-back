using Microsoft.Extensions.Options;

namespace Sensus.Infrastructure.Authentication;

public sealed class JwtSettingsValidator : IValidateOptions<JwtSettings>
{
    public ValidateOptionsResult Validate(string? name, JwtSettings options)
    {
        if (string.IsNullOrWhiteSpace(options.Secret))
        {
            return ValidateOptionsResult.Fail("JwtSettings:Secret es obligatorio y no puede estar vacío.");
        }

        if (options.Secret.Length < 32)
        {
            return ValidateOptionsResult.Fail("JwtSettings:Secret debe tener al menos 32 caracteres.");
        }

        if (string.IsNullOrWhiteSpace(options.Issuer))
        {
            return ValidateOptionsResult.Fail("JwtSettings:Issuer es obligatorio.");
        }

        if (string.IsNullOrWhiteSpace(options.Audience))
        {
            return ValidateOptionsResult.Fail("JwtSettings:Audience es obligatorio.");
        }

        if (options.ExpiryMinutes <= 0)
        {
            return ValidateOptionsResult.Fail("JwtSettings:ExpiryMinutes debe ser mayor a 0.");
        }

        if (options.GuestExpiryMinutes is < 1 or > 15)
        {
            return ValidateOptionsResult.Fail("JwtSettings:GuestExpiryMinutes debe estar entre 1 y 15 minutos.");
        }

        return ValidateOptionsResult.Success;
    }
}
