using FluentValidation;
using Sensus.Application.Media.Commands.UploadMedia;

namespace Sensus.Application.Media.Commands.UploadMedia;

public sealed class UploadMediaCommandValidator : AbstractValidator<UploadMediaCommand>
{
    private const long MaxFileSizeBytes = 4_500_000; // 4,5 MB

    private static readonly HashSet<string> AllowedMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/png",
        "image/webp"
    };

    public UploadMediaCommandValidator()
    {
        RuleFor(x => x.ImageBytes)
            .NotEmpty().WithMessage("La imagen es obligatoria.")
            .Must(BeWithinSizeLimit).WithMessage($"El archivo no puede exceder los 4,5 MB.")
            .Must(HaveValidImageSignature).WithMessage("El tipo de archivo no es una imagen válida (JPEG, PNG o WebP).");

        RuleFor(x => x.ContentType)
            .NotEmpty().WithMessage("El tipo de contenido es obligatorio.")
            .Must(BeAllowedMimeType).WithMessage("Solo se permiten imágenes JPEG, PNG y WebP.");
    }

    private static bool BeWithinSizeLimit(byte[]? bytes)
    {
        return bytes is not null && bytes.Length <= MaxFileSizeBytes;
    }

    private static bool HaveValidImageSignature(byte[]? bytes)
    {
        if (bytes is null || bytes.Length < 4)
        {
            return false;
        }

        // JPEG: FF D8 FF
        if (bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF)
        {
            return true;
        }

        // PNG: 89 50 4E 47
        if (bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47)
        {
            return true;
        }

        // WebP: RIFF .... WEBP
        if (bytes.Length >= 12 &&
            bytes[0] == 0x52 && bytes[1] == 0x49 && bytes[2] == 0x46 && bytes[3] == 0x46 &&
            bytes[8] == 0x57 && bytes[9] == 0x45 && bytes[10] == 0x42 && bytes[11] == 0x50)
        {
            return true;
        }

        return false;
    }

    private static bool BeAllowedMimeType(string? contentType)
    {
        return !string.IsNullOrWhiteSpace(contentType) && AllowedMimeTypes.Contains(contentType.Trim());
    }
}
