using Sensus.Domain.Common;
using Sensus.Domain.Enums;

namespace Sensus.Domain.Entities;

/// <summary>
/// Categorías de interés explícitas del usuario registrado para ordenar el feed de promociones
/// (Spec 054, FR-054-03, FR-054-04). La lista vacía es válida.
/// </summary>
public sealed class UserInterests : BaseEntity, IAggregateRoot
{
    public const int MaxCategories = 5;

    private readonly List<OfferCategory> _categories = [];

    public Guid UserId { get; private set; }
    public IReadOnlyList<OfferCategory> Categories => _categories.AsReadOnly();
    public DateTime UpdatedAtUtc { get; private set; }

    private UserInterests() : base()
    {
    }

    private UserInterests(Guid id, Guid userId, IEnumerable<OfferCategory> categories, DateTime updatedAtUtc)
        : base(id)
    {
        UserId = userId;
        _categories.AddRange(categories);
        UpdatedAtUtc = updatedAtUtc;
    }

    public static Result<UserInterests> Create(
        Guid userId,
        IEnumerable<OfferCategory> categories,
        DateTime updatedAtUtc)
    {
        if (userId == Guid.Empty)
        {
            return Result.Failure<UserInterests>(
                Error.Validation("UserInterests.UserIdRequired", "El identificador del usuario es obligatorio."));
        }

        Result<List<OfferCategory>> validated = ValidateCategories(categories);
        if (validated.IsFailure)
        {
            return Result.Failure<UserInterests>(validated.Error);
        }

        return Result.Success(new UserInterests(Guid.NewGuid(), userId, validated.Value, updatedAtUtc));
    }

    public static UserInterests Reconstitute(
        Guid id,
        Guid userId,
        IEnumerable<OfferCategory> categories,
        DateTime updatedAtUtc) =>
        new(id, userId, categories, updatedAtUtc);

    public Result Replace(IEnumerable<OfferCategory> categories, DateTime updatedAtUtc)
    {
        Result<List<OfferCategory>> validated = ValidateCategories(categories);
        if (validated.IsFailure)
        {
            return Result.Failure(validated.Error);
        }

        _categories.Clear();
        _categories.AddRange(validated.Value);
        UpdatedAtUtc = updatedAtUtc;
        return Result.Success();
    }

    private static Result<List<OfferCategory>> ValidateCategories(IEnumerable<OfferCategory>? categories)
    {
        if (categories is null)
        {
            return Result.Failure<List<OfferCategory>>(
                Error.Validation("UserInterests.CategoriesRequired", "La lista de categorías no puede ser nula."));
        }

        var list = new List<OfferCategory>();
        var seen = new HashSet<OfferCategory>();

        foreach (OfferCategory category in categories)
        {
            if (!Enum.IsDefined(category))
            {
                return Result.Failure<List<OfferCategory>>(
                    Error.Validation("UserInterests.InvalidCategory", "Una o más categorías no pertenecen al catálogo OfferCategory."));
            }

            if (!seen.Add(category))
            {
                return Result.Failure<List<OfferCategory>>(
                    Error.Validation("UserInterests.DuplicateCategory", "No se permiten categorías duplicadas en los intereses."));
            }

            list.Add(category);
            if (list.Count > MaxCategories)
            {
                return Result.Failure<List<OfferCategory>>(
                    Error.Validation("UserInterests.TooManyCategories", $"Solo se permiten hasta {MaxCategories} categorías de interés."));
            }
        }

        return Result.Success(list);
    }
}
