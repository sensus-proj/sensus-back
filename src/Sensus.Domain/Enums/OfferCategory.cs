using System.Globalization;
using System.Text;

namespace Sensus.Domain.Enums;

/// <summary>
/// Catálogo oficial de categorías de producto para ofertas, intereses del usuario y feed de promociones
/// (Specs 034, 042, 054 y 055). Es independiente de <see cref="MerchantCategory"/>.
/// </summary>
public enum OfferCategory
{
    Grocery = 1,
    CleaningHome = 2,
    Perishables = 3,
    Dairy = 4,
    Food = 5,
    Bakery = 6,
    Beverages = 7,
    Pets = 8,
    Pharmacy = 9
}

/// <summary>
/// Operaciones de consulta, validación con lista de permitidos y normalización sobre <see cref="OfferCategory"/>.
/// </summary>
public static class OfferCategoryCatalog
{
    private static readonly OfferCategory[] Categories = Enum.GetValues<OfferCategory>();
    private static readonly string[] CategoryNames = Categories.Select(c => c.ToString()).ToArray();

    private static readonly Dictionary<string, OfferCategory> LegacySpanishMappings =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["abarrotes"] = OfferCategory.Grocery,
            ["limpieza y hogar"] = OfferCategory.CleaningHome,
            ["perecederos"] = OfferCategory.Perishables,
            ["lacteos"] = OfferCategory.Dairy,
            ["comida"] = OfferCategory.Food,
            ["panaderia"] = OfferCategory.Bakery,
            ["bebidas"] = OfferCategory.Beverages,
            ["mascotas"] = OfferCategory.Pets,
            ["farmacia"] = OfferCategory.Pharmacy
        };

    /// <summary>Todas las categorías válidas del catálogo.</summary>
    public static IReadOnlyList<OfferCategory> All => Categories;

    /// <summary>Nombres canónicos en inglés del catálogo.</summary>
    public static IReadOnlyList<string> Names => CategoryNames;

    /// <summary>
    /// Valida estrictamente contra la lista de permitidos de <see cref="OfferCategory"/> (sin aceptar valores numéricos).
    /// </summary>
    public static bool TryParse(string? value, out OfferCategory category)
    {
        category = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        string trimmed = value.Trim();
        if (!char.IsAsciiLetter(trimmed[0]))
        {
            return false;
        }

        return Enum.TryParse(trimmed, ignoreCase: true, out category) && Enum.IsDefined(category);
    }

    /// <summary>
    /// Convierte un código oficial o un nombre legado en español al valor canónico de <see cref="OfferCategory"/>
    /// (FR-034-09). Devuelve <c>false</c> para valores no reconocibles.
    /// </summary>
    public static bool TryNormalizeLegacy(string? value, out OfferCategory category)
    {
        if (TryParse(value, out category))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            category = default;
            return false;
        }

        string normalized = RemoveDiacritics(value.Trim());
        return LegacySpanishMappings.TryGetValue(normalized, out category);
    }

    /// <summary>
    /// Devuelve la etiqueta visible y el alias sin acentos en español para indexación y búsqueda textual (Spec 041).
    /// </summary>
    public static string GetSpanishName(OfferCategory category) =>
        category switch
        {
            OfferCategory.Grocery => "Abarrotes",
            OfferCategory.CleaningHome => "Limpieza y Hogar",
            OfferCategory.Perishables => "Perecederos",
            OfferCategory.Dairy => "Lácteos",
            OfferCategory.Food => "Comida",
            OfferCategory.Bakery => "Panadería",
            OfferCategory.Beverages => "Bebidas",
            OfferCategory.Pets => "Mascotas",
            OfferCategory.Pharmacy => "Farmacia",
            _ => category.ToString()
        };

    /// <summary>
    /// Elimina marcas diacríticas manteniendo los caracteres base en forma Unicode C.
    /// </summary>
    public static string RemoveDiacritics(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        string decomposed = text.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (char ch in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(ch);
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }
}
