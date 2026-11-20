using FluentValidation;
using Sensus.Domain.Entities;
using Sensus.Domain.Enums;

namespace Sensus.Application.Users.Commands.ReplaceMyInterests;

public sealed class ReplaceMyInterestsCommandValidator : AbstractValidator<ReplaceMyInterestsCommand>
{
    public ReplaceMyInterestsCommandValidator()
    {
        RuleFor(x => x.Categories)
            .NotNull().WithMessage("La lista de categorías es obligatoria.")
            .Must(categories => categories is null || categories.Count <= UserInterests.MaxCategories)
            .WithMessage($"Solo se permiten hasta {UserInterests.MaxCategories} categorías de interés.")
            .Must(HaveNoDuplicateCategories)
            .WithMessage("No se permiten categorías duplicadas en los intereses.");

        RuleForEach(x => x.Categories)
            .Must(c => OfferCategoryCatalog.TryParse(c, out _))
            .WithMessage($"Cada categoría debe pertenecer al catálogo OfferCategory: {string.Join(", ", OfferCategoryCatalog.Names)}.");
    }

    private static bool HaveNoDuplicateCategories(IReadOnlyList<string>? categories)
    {
        if (categories is null)
        {
            return true;
        }

        var seen = new HashSet<OfferCategory>();
        foreach (string raw in categories)
        {
            if (OfferCategoryCatalog.TryParse(raw, out OfferCategory parsed) && !seen.Add(parsed))
            {
                return false;
            }
        }

        return true;
    }
}
