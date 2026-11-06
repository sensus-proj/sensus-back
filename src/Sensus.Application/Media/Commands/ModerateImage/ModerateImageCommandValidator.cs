using FluentValidation;

namespace Sensus.Application.Media.Commands.ModerateImage;

public sealed class ModerateImageCommandValidator : AbstractValidator<ModerateImageCommand>
{
    public ModerateImageCommandValidator()
    {
        RuleFor(x => x.ImageBase64)
            .NotEmpty().WithMessage("La imagen en formato Base64 es obligatoria.")
            .Must(BeValidBase64).WithMessage("El contenido proporcionado no es una cadena Base64 válida.");
    }

    private static bool BeValidBase64(string base64)
    {
        if (string.IsNullOrWhiteSpace(base64)) return false;
        var clean = base64.Contains(',') ? base64[(base64.IndexOf(',') + 1)..] : base64;
        clean = clean.Trim();
        var buffer = new Span<byte>(new byte[clean.Length]);
        return Convert.TryFromBase64String(clean, buffer, out _);
    }
}
