using FluentValidation;

namespace Sensus.Application.Ai.Queries.GetConversation;

/// <summary>
/// Validación de borde de la consulta paginada de una conversación (FR-046-01; AGENTS.md §9:
/// 20 por defecto y 100 como máximo).
/// </summary>
public sealed class GetConversationQueryValidator : AbstractValidator<GetConversationQuery>
{
    public const int MaxPageSize = 100;

    public GetConversationQueryValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1)
            .WithMessage("La página debe ser mayor o igual a 1.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, MaxPageSize)
            .WithMessage($"El tamaño de página debe estar entre 1 y {MaxPageSize}.");
    }
}
