using FluentValidation;
using Sensus.Domain.Common;

namespace Sensus.Application.Ai.Commands.CreateConversation;

public sealed class CreateOrResumeConversationCommandValidator : AbstractValidator<CreateOrResumeConversationCommand>
{
    public CreateOrResumeConversationCommandValidator()
    {
        RuleFor(x => x.ScreenContext)
            .Must(s => string.IsNullOrWhiteSpace(s) || KnownScreenContexts.IsValid(s))
            .WithMessage("La pantalla de origen (ScreenContext) no es válida.");
    }
}
