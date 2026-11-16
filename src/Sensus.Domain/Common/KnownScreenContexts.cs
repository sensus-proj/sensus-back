namespace Sensus.Domain.Common;

/// <summary>
/// Catálogo de pantallas reconocidas y validadas en el cliente para el contexto de Sensi IA (FR-046-04).
/// Protege contra inyecciones de instrucciones estructuradas en el campo ScreenContext.
/// </summary>
public static class KnownScreenContexts
{
    public const string Home = "home";
    public const string Map = "map";
    public const string OfferDetail = "offer_detail";
    public const string MyClaims = "my_claims";
    public const string MerchantInventory = "merchant_inventory";
    public const string MerchantProfile = "merchant_profile";
    public const string UserProfile = "user_profile";
    public const string Settings = "settings";

    public static readonly HashSet<string> All = new(StringComparer.OrdinalIgnoreCase)
    {
        Home,
        Map,
        OfferDetail,
        MyClaims,
        MerchantInventory,
        MerchantProfile,
        UserProfile,
        Settings
    };

    public static bool IsValid(string? screenContext)
    {
        if (string.IsNullOrWhiteSpace(screenContext))
        {
            return false;
        }

        return All.Contains(screenContext.Trim());
    }

    public static string? Normalize(string? screenContext)
    {
        if (string.IsNullOrWhiteSpace(screenContext))
        {
            return null;
        }

        var clean = screenContext.Trim().ToLowerInvariant();
        return All.Contains(clean) ? clean : null;
    }
}
