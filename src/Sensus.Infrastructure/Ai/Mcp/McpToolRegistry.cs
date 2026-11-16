using Sensus.Application.Common.Interfaces;
using Sensus.Domain.Enums;

namespace Sensus.Infrastructure.Ai.Mcp;

/// <summary>
/// Registro centralizado y determinista del catálogo de herramientas MCP de Sensi IA (FR-046-06).
/// Garantiza el orden alfabético estricto de las definiciones para no invalidar el Prompt Caching de OpenAI.
/// </summary>
public sealed class McpToolRegistry : IMcpToolRegistry
{
    public const string SearchOffersToolName = SensiToolNames.SearchOffers;
    public const string GetOfferDetailToolName = SensiToolNames.GetOfferDetail;
    public const string GetMyAccountToolName = SensiToolNames.GetMyAccount;
    public const string GetMyClaimsToolName = SensiToolNames.GetMyClaims;
    public const string GetMyMerchantOffersToolName = SensiToolNames.GetMyMerchantOffers;

    private static readonly List<McpToolDefinition> AllTools = new()
    {
        new McpToolDefinition(
            SearchOffersToolName,
            "Busca ofertas y productos activos en comercios locales filtrando por palabra clave, categoría, presupuesto máximo y radio.",
            new
            {
                type = "object",
                properties = new
                {
                    query = new { type = "string", description = "Término de búsqueda o producto (ej. pizza, café, tenis)" },
                    category = new { type = "string", description = "Categoría opcional: Restaurantes, Cafeterias, Supermercados, Farmacias, Moda, Tecnologia, Hogar, Belleza, Deportes, Otros" },
                    maxBudgetMxn = new { type = "number", description = "Presupuesto máximo en MXN (pesos mexicanos)" },
                    lat = new { type = "number", description = "Latitud del usuario para búsqueda cercana" },
                    lon = new { type = "number", description = "Longitud del usuario para búsqueda cercana" },
                    radiusKm = new { type = "integer", description = "Radio de búsqueda en kilómetros (entre 1 y 5, defecto 3)" }
                }
            },
            AiScope.OffersRead),

        new McpToolDefinition(
            GetOfferDetailToolName,
            "Obtiene el detalle completo, vigencia, precio con descuento, stock y condiciones de una oferta específica por su ID.",
            new
            {
                type = "object",
                properties = new
                {
                    offerId = new { type = "string", description = "Identificador único GUID de la oferta" }
                },
                required = new[] { "offerId" }
            },
            AiScope.OffersRead),

        new McpToolDefinition(
            GetMyAccountToolName,
            "Consulta datos básicos del perfil del usuario autenticado (nombre/alias, rol activo, ciudad general y preferencias de notificación).",
            new
            {
                type = "object",
                properties = new { }
            },
            AiScope.AccountRead),

        new McpToolDefinition(
            GetMyClaimsToolName,
            "Consulta el historial de apartados de ofertas (códigos QR) del usuario autenticado.",
            new
            {
                type = "object",
                properties = new
                {
                    status = new { type = "string", description = "Filtro opcional de estado: Active, Redeemed, Expired, Cancelled" },
                    limit = new { type = "integer", description = "Cantidad máxima de apartados a recuperar (máx. 10, defecto 5)" }
                }
            },
            AiScope.ClaimsRead),

        new McpToolDefinition(
            GetMyMerchantOffersToolName,
            "Consulta las ofertas publicadas por el comercio del comerciante autenticado. Requiere rol de comerciante.",
            new
            {
                type = "object",
                properties = new
                {
                    status = new { type = "string", description = "Filtro opcional de estado de oferta: Draft, Published, Paused, Expired" },
                    limit = new { type = "integer", description = "Cantidad máxima de ofertas a consultar (máx. 20, defecto 10)" }
                }
            },
            AiScope.MerchantRead)
    };

    public IReadOnlyList<McpToolDefinition> GetAllowedTools(IReadOnlySet<AiScope> userScopes)
    {
        return AllTools
            .Where(t => userScopes.Contains(t.RequiredScope))
            .OrderBy(t => t.Name, StringComparer.Ordinal)
            .ToList();
    }
}
