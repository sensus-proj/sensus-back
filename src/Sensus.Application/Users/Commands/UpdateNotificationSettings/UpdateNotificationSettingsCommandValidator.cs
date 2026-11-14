using FluentValidation;
using Sensus.Application.Users.Commands.UpdateNotificationSettings;

namespace Sensus.Application.Users.Commands.UpdateNotificationSettings;

public sealed class UpdateNotificationSettingsCommandValidator : AbstractValidator<UpdateNotificationSettingsCommand>
{
    public UpdateNotificationSettingsCommandValidator()
    {
        RuleFor(x => x.RadiusKm)
            .InclusiveBetween(1, 50)
            .When(x => x.RadiusKm.HasValue)
            .WithMessage("El radio debe estar entre 1 y 50 km.");
    }
}
