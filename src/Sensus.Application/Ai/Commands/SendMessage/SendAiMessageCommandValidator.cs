using FluentValidation;
using Sensus.Domain.Common;

namespace Sensus.Application.Ai.Commands.SendMessage;

public sealed class SendAiMessageCommandValidator : AbstractValidator<SendAiMessageCommand>
{
    public SendAiMessageCommandValidator()
    {
        RuleFor(x => x.Message)
            .NotEmpty().WithMessage("El mensaje no puede estar vacío.");

        RuleFor(x => x.ScreenContext)
            .Must(s => string.IsNullOrWhiteSpace(s) || KnownScreenContexts.IsValid(s))
            .WithMessage("La pantalla de origen (ScreenContext) no es válida.");

        RuleFor(x => x.RadiusKm)
            .InclusiveBetween(1.0, 5.0)
            .When(x => x.RadiusKm.HasValue)
            .WithMessage("El radio de búsqueda debe estar entre 1.0 y 5.0 km.");
    }
}
