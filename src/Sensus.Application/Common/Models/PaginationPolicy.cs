namespace Sensus.Application.Common.Models;

/// <summary>
/// Política estándar de paginación para consultas listables (Spec 010).
/// </summary>
public static class PaginationPolicy
{
    public const int DefaultPageNumber = 1;
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;

    public static (int PageNumber, int PageSize) Normalize(int? page, int? pageSize)
    {
        int normalizedPage = page is null or < 1 ? DefaultPageNumber : page.Value;
        int normalizedSize = pageSize is null or < 1
            ? DefaultPageSize
            : Math.Min(pageSize.Value, MaxPageSize);

        return (normalizedPage, normalizedSize);
    }

    public static bool IsValidPageNumber(int pageNumber) => pageNumber >= 1;

    public static bool IsValidPageSize(int pageSize) => pageSize >= 1 && pageSize <= MaxPageSize;
}
