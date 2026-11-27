using FluentValidation;
using Microsoft.Extensions.Options;
using Sensus.Application.Common.Settings;

namespace Sensus.Application.Users.Commands.UpdateProfile;

public sealed class UpdateProfileCommandValidator : AbstractValidator<UpdateProfileCommand>
{
    private const string CloudinaryCdnHost = "res.cloudinary.com";

    public UpdateProfileCommandValidator(IOptions<CloudinaryAccountSettings>? cloudinaryOptions = null)
    {
        string cloudName = cloudinaryOptions?.Value.CloudName?.Trim().Trim('/') ?? string.Empty;

        RuleFor(x => x.NombreCompleto)
            .NotEmpty().WithMessage("El nombre completo es obligatorio.")
            .MinimumLength(2).WithMessage("El nombre completo debe tener al menos 2 caracteres.")
            .MaximumLength(100).WithMessage("El nombre completo no puede exceder 100 caracteres.")
            .Must(nombre => !nombre.Any(char.IsControl)).WithMessage("El nombre completo no puede contener caracteres de control.");

        When(x => !string.IsNullOrWhiteSpace(x.FotoPerfilUrl), () =>
        {
            RuleFor(x => x.FotoPerfilUrl!)
                .Must(url => IsAllowedCloudinaryAssetUrl(url, cloudName))
                .WithMessage("La foto de perfil debe ser una URL HTTPS válida de Cloudinary perteneciente a la cuenta de Sensus.");
        });
    }

    private static bool IsAllowedCloudinaryAssetUrl(string rawUrl, string cloudName)
    {
        if (string.IsNullOrWhiteSpace(cloudName) ||
            !Uri.TryCreate(rawUrl.Trim(), UriKind.Absolute, out Uri? uri) ||
            uri.Scheme != Uri.UriSchemeHttps ||
            !string.Equals(uri.Host, CloudinaryCdnHost, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string expectedPrefix = $"/{cloudName}/";
        return uri.AbsolutePath.StartsWith(expectedPrefix, StringComparison.OrdinalIgnoreCase);
    }
}
