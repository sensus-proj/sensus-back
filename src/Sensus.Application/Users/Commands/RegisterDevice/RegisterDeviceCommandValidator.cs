using FluentValidation;
using Sensus.Application.Users.Commands.RegisterDevice;

namespace Sensus.Application.Users.Commands.RegisterDevice;

public sealed class RegisterDeviceCommandValidator : AbstractValidator<RegisterDeviceCommand>
{
    public RegisterDeviceCommandValidator()
    {
        RuleFor(x => x.DeviceId)
            .NotEmpty().WithMessage("El identificador de dispositivo es obligatorio.")
            .MaximumLength(100).WithMessage("El deviceId no puede exceder 100 caracteres.");

        RuleFor(x => x.Platform)
            .NotEmpty().WithMessage("La plataforma es obligatoria.")
            .Must(BeValidPlatform).WithMessage("La plataforma debe ser 'Android', 'iOS' o 'Web'.");

        RuleFor(x => x.Token)
            .NotEmpty().WithMessage("El token de dispositivo es obligatorio.")
            .MinimumLength(20).WithMessage("El token de dispositivo es demasiado corto.");
    }

    private static bool BeValidPlatform(string platform)
    {
        return platform switch
        {
            "Android" or "iOS" or "Web" => true,
            _ => false
        };
    }
}
