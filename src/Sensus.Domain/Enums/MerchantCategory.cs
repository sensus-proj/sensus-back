namespace Sensus.Domain.Enums;

/// <summary>
/// Catálogo de categorías comerciales del local (FR-005-02, FR-005-05, FR-031-03 y FR-007-03).
/// <see cref="Otros"/> es el valor por defecto para no romper documentos ni clientes previos.
/// </summary>
public enum MerchantCategory
{
    Otros = 0,
    Restaurantes = 1,
    Cafeterias = 2,
    Supermercados = 3,
    Farmacias = 4,
    Moda = 5,
    Tecnologia = 6,
    Hogar = 7,
    Belleza = 8,
    Deportes = 9
}

/// <summary>
/// Operaciones de consulta y validación sobre el catálogo de categorías comerciales.
/// </summary>
public static class MerchantCategoryCatalog
{
    private static readonly MerchantCategory[] Categories = Enum.GetValues<MerchantCategory>();
    private static readonly string[] CategoryNames = Categories.Select(c => c.ToString()).ToArray();

    /// <summary>
    /// Categoría aplicada cuando el cliente no la informa.
    /// </summary>
    public static MerchantCategory Default => MerchantCategory.Otros;

    /// <summary>
    /// Todas las categorías válidas del catálogo.
    /// </summary>
    public static IReadOnlyList<MerchantCategory> All => Categories;

    /// <summary>
    /// Nombres de las categorías válidas, en el orden del catálogo.
    /// </summary>
    public static IReadOnlyList<string> Names => CategoryNames;

    /// <summary>
    /// Intenta convertir un nombre de categoría (sin distinguir mayúsculas) en un valor del catálogo.
    /// </summary>
    public static bool TryParse(string? value, out MerchantCategory category)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            category = Default;
            return false;
        }

        return Enum.TryParse(value.Trim(), ignoreCase: true, out category) && Enum.IsDefined(category);
    }
}
