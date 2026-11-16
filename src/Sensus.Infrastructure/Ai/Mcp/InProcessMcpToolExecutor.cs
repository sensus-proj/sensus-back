using System.Text.Json;
using Microsoft.Extensions.Logging;
using Sensus.Application.Common.Interfaces;
using Sensus.Domain.Entities;
using Sensus.Domain.Enums;
using Sensus.Domain.Repositories;

namespace Sensus.Infrastructure.Ai.Mcp;

/// <summary>
/// Ejecutor in-process de herramientas MCP de solo lectura con validación estricta de scopes y saneamiento anti-inyección indirecta (FR-046-06, FR-046-09).
/// </summary>
public sealed class InProcessMcpToolExecutor : IMcpToolExecutor
{
    private readonly IOfferRepository _offerRepository;
    private readonly IComercioRepository _comercioRepository;
    private readonly IUserRepository _userRepository;
    private readonly IOfferClaimRepository _offerClaimRepository;
    private readonly ILogger<InProcessMcpToolExecutor> _logger;

    public InProcessMcpToolExecutor(
        IOfferRepository offerRepository,
        IComercioRepository comercioRepository,
        IUserRepository userRepository,
        IOfferClaimRepository offerClaimRepository,
        ILogger<InProcessMcpToolExecutor> logger)
    {
        _offerRepository = offerRepository;
        _comercioRepository = comercioRepository;
        _userRepository = userRepository;
        _offerClaimRepository = offerClaimRepository;
        _logger = logger;
    }

    public async Task<McpToolExecutionResult> ExecuteAsync(
        McpToolCall call,
        Guid callerUserId,
        IReadOnlySet<AiScope> callerScopes,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Ejecutando herramienta MCP {ToolName} para usuario {UserId}", call.ToolName, callerUserId);

        try
        {
            return call.ToolName switch
            {
                McpToolRegistry.SearchOffersToolName => await ExecuteSearchOffersAsync(call, callerScopes, cancellationToken),
                McpToolRegistry.GetOfferDetailToolName => await ExecuteGetOfferDetailAsync(call, callerScopes, cancellationToken),
                McpToolRegistry.GetMyAccountToolName => await ExecuteGetMyAccountAsync(call, callerUserId, callerScopes, cancellationToken),
                McpToolRegistry.GetMyClaimsToolName => await ExecuteGetMyClaimsAsync(call, callerUserId, callerScopes, cancellationToken),
                McpToolRegistry.GetMyMerchantOffersToolName => await ExecuteGetMyMerchantOffersAsync(call, callerUserId, callerScopes, cancellationToken),
                _ => new McpToolExecutionResult(call.Id, call.ToolName, "{}", false, $"Herramienta desconocida '{call.ToolName}'.")
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al ejecutar la herramienta MCP {ToolName}", call.ToolName);
            return new McpToolExecutionResult(call.Id, call.ToolName, "{}", false, "Error interno al ejecutar la herramienta.");
        }
    }

    private async Task<McpToolExecutionResult> ExecuteSearchOffersAsync(
        McpToolCall call,
        IReadOnlySet<AiScope> callerScopes,
        CancellationToken cancellationToken)
    {
        if (!callerScopes.Contains(AiScope.OffersRead))
        {
            return new McpToolExecutionResult(call.Id, call.ToolName, "{}", false, "Alcance insuficiente para buscar ofertas.");
        }

        using var doc = ParseJsonArguments(call.ArgumentsJson);
        var root = doc?.RootElement;

        string? query = GetOptionalString(root, "query");
        string? categoryStr = GetOptionalString(root, "category");
        decimal? maxBudgetMxn = GetOptionalDecimal(root, "maxBudgetMxn");
        double? lat = GetOptionalDouble(root, "lat");
        double? lon = GetOptionalDouble(root, "lon");
        int? radiusKm = GetOptionalInt(root, "radiusKm");

        MerchantCategory? category = null;
        if (!string.IsNullOrWhiteSpace(categoryStr) && Enum.TryParse<MerchantCategory>(categoryStr, true, out var cat))
        {
            category = cat;
        }

        IReadOnlyList<Offer> offers;
        if (lat.HasValue && lon.HasValue)
        {
            var clampedRadiusKm = Math.Clamp(radiusKm ?? 3, 1, 5);
            var nearbyPage = await _offerRepository.GetNearbyAsync(
                lat.Value,
                lon.Value,
                clampedRadiusKm * 1000,
                category,
                1,
                15,
                DateTime.UtcNow,
                cancellationToken);

            offers = nearbyPage.Items.Select(item => item.Offer).ToList();
            if (maxBudgetMxn.HasValue && maxBudgetMxn.Value > 0)
            {
                offers = offers.Where(o => o.DiscountedPrice <= maxBudgetMxn.Value).ToList();
            }
        }
        else
        {
            offers = await _offerRepository.SearchAsync(query, category, maxBudgetMxn, 15, cancellationToken);
        }

        var results = offers.Select(o => new
        {
            id = o.Id.ToString(),
            title = SanitizeIndirectInjection(o.Title),
            description = SanitizeIndirectInjection(o.Description),
            originalPrice = o.OriginalPrice,
            discountedPrice = o.DiscountedPrice,
            discountPercentage = o.DiscountPercentage,
            stock = o.Stock,
            endsAtUtc = o.EndsAt,
            category = o.Category,
            merchantId = o.MerchantId.ToString()
        }).ToList();

        var json = JsonSerializer.Serialize(new { offers = results, count = results.Count });
        return new McpToolExecutionResult(call.Id, call.ToolName, json, true);
    }

    private async Task<McpToolExecutionResult> ExecuteGetOfferDetailAsync(
        McpToolCall call,
        IReadOnlySet<AiScope> callerScopes,
        CancellationToken cancellationToken)
    {
        if (!callerScopes.Contains(AiScope.OffersRead))
        {
            return new McpToolExecutionResult(call.Id, call.ToolName, "{}", false, "Alcance insuficiente para consultar detalle de oferta.");
        }

        using var doc = ParseJsonArguments(call.ArgumentsJson);
        var root = doc?.RootElement;
        var offerIdStr = GetOptionalString(root, "offerId");

        if (string.IsNullOrWhiteSpace(offerIdStr) || !Guid.TryParse(offerIdStr, out var offerId))
        {
            return new McpToolExecutionResult(call.Id, call.ToolName, "{}", false, "offerId inválido o no proporcionado.");
        }

        var offer = await _offerRepository.GetByIdAsync(offerId, cancellationToken);
        if (offer is null || !offer.IsActive(DateTime.UtcNow))
        {
            var notFoundJson = JsonSerializer.Serialize(new { found = false, message = "La oferta no existe o no se encuentra vigente." });
            return new McpToolExecutionResult(call.Id, call.ToolName, notFoundJson, true);
        }

        var comercio = await _comercioRepository.GetByIdAsync(offer.MerchantId, cancellationToken);

        var detail = new
        {
            found = true,
            id = offer.Id.ToString(),
            title = SanitizeIndirectInjection(offer.Title),
            description = SanitizeIndirectInjection(offer.Description),
            originalPrice = offer.OriginalPrice,
            discountedPrice = offer.DiscountedPrice,
            discountPercentage = offer.DiscountPercentage,
            stock = offer.Stock,
            startsAtUtc = offer.StartsAt,
            endsAtUtc = offer.EndsAt,
            category = offer.Category,
            merchant = comercio is null ? null : new
            {
                id = comercio.Id.ToString(),
                nombre = SanitizeIndirectInjection(comercio.NombreComercial),
                direccion = SanitizeIndirectInjection(comercio.Direccion),
                codigoPostal = comercio.CodigoPostal,
                categoria = comercio.Categoria.ToString()
            }
        };

        var json = JsonSerializer.Serialize(detail);
        return new McpToolExecutionResult(call.Id, call.ToolName, json, true);
    }

    private async Task<McpToolExecutionResult> ExecuteGetMyAccountAsync(
        McpToolCall call,
        Guid callerUserId,
        IReadOnlySet<AiScope> callerScopes,
        CancellationToken cancellationToken)
    {
        if (!callerScopes.Contains(AiScope.AccountRead))
        {
            return new McpToolExecutionResult(call.Id, call.ToolName, "{}", false, "Alcance insuficiente para consultar cuenta.");
        }

        var user = await _userRepository.GetByIdAsync(callerUserId, cancellationToken);
        if (user is null)
        {
            return new McpToolExecutionResult(call.Id, call.ToolName, "{}", false, "Usuario no encontrado.");
        }

        // Principio de privacidad: NUNCA devolver passwordHash, securityStamp, correo completo ni tokens
        // Plan L84: solo nombre de pila (primer componente) para no filtrar PII de nombre completo hacia OpenAI
        var firstName = !string.IsNullOrWhiteSpace(user.NombreCompleto)
            ? user.NombreCompleto.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault() ?? user.NombreCompleto
            : string.Empty;

        var accountInfo = new
        {
            userId = user.Id.ToString(),
            nombre = SanitizeIndirectInjection(firstName),
            rol = user.Roles.FirstOrDefault() ?? RoleType.Consumidor,
            activo = user.Estatus,
            fechaRegistroUtc = user.FechaRegistro
        };

        var json = JsonSerializer.Serialize(accountInfo);
        return new McpToolExecutionResult(call.Id, call.ToolName, json, true);
    }

    private async Task<McpToolExecutionResult> ExecuteGetMyClaimsAsync(
        McpToolCall call,
        Guid callerUserId,
        IReadOnlySet<AiScope> callerScopes,
        CancellationToken cancellationToken)
    {
        if (!callerScopes.Contains(AiScope.ClaimsRead))
        {
            return new McpToolExecutionResult(call.Id, call.ToolName, "{}", false, "Alcance insuficiente para consultar apartados.");
        }

        using var doc = ParseJsonArguments(call.ArgumentsJson);
        var root = doc?.RootElement;
        var statusStr = GetOptionalString(root, "status");
        var limit = GetOptionalInt(root, "limit") ?? 5;

        OfferClaimStatus? statusFilter = null;
        if (!string.IsNullOrWhiteSpace(statusStr) && Enum.TryParse<OfferClaimStatus>(statusStr, true, out var s))
        {
            statusFilter = s;
        }

        var claims = await _offerClaimRepository.GetByUserIdAsync(callerUserId, statusFilter, Math.Clamp(limit, 1, 10), cancellationToken);

        var results = claims.Select(c => new
        {
            claimId = c.Id.ToString(),
            offerId = c.OfferId.ToString(),
            status = c.Status.ToString(),
            createdAtUtc = c.CreatedAtUtc,
            expiresAtUtc = c.ExpiresAtUtc,
            redeemedAtUtc = c.RedeemedAtUtc
        }).ToList();

        var json = JsonSerializer.Serialize(new { claims = results, count = results.Count });
        return new McpToolExecutionResult(call.Id, call.ToolName, json, true);
    }

    private async Task<McpToolExecutionResult> ExecuteGetMyMerchantOffersAsync(
        McpToolCall call,
        Guid callerUserId,
        IReadOnlySet<AiScope> callerScopes,
        CancellationToken cancellationToken)
    {
        if (!callerScopes.Contains(AiScope.MerchantRead))
        {
            return new McpToolExecutionResult(call.Id, call.ToolName, "{}", false, "Alcance de comerciante requerido.");
        }

        var comercio = await _comercioRepository.GetByUsuarioIdAsync(callerUserId, cancellationToken);
        if (comercio is null)
        {
            var notFoundJson = JsonSerializer.Serialize(new { error = "El usuario autenticado no tiene un comercio registrado." });
            return new McpToolExecutionResult(call.Id, call.ToolName, notFoundJson, true);
        }

        using var doc = ParseJsonArguments(call.ArgumentsJson);
        var root = doc?.RootElement;
        var statusStr = GetOptionalString(root, "status");
        var limit = GetOptionalInt(root, "limit") ?? 10;

        OfferStatus? statusFilter = null;
        if (!string.IsNullOrWhiteSpace(statusStr) && Enum.TryParse<OfferStatus>(statusStr, true, out var os))
        {
            statusFilter = os;
        }

        var (items, totalCount) = await _offerRepository.GetByMerchantIdPagedAsync(
            comercio.Id,
            statusFilter,
            1,
            Math.Clamp(limit, 1, 20),
            cancellationToken);

        var results = items.Select(o => new
        {
            id = o.Id.ToString(),
            title = SanitizeIndirectInjection(o.Title),
            description = SanitizeIndirectInjection(o.Description),
            originalPrice = o.OriginalPrice,
            discountedPrice = o.DiscountedPrice,
            stock = o.Stock,
            status = o.Status.ToString(),
            endsAtUtc = o.EndsAt
        }).ToList();

        var json = JsonSerializer.Serialize(new { totalCount, offers = results });
        return new McpToolExecutionResult(call.Id, call.ToolName, json, true);
    }

    public static string SanitizeIndirectInjection(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return string.Empty;
        }

        var text = input.Length > 500 ? input[..500] : input;

        text = text.Replace("<contexto>", "[contexto]", StringComparison.OrdinalIgnoreCase)
                   .Replace("</contexto>", "[/contexto]", StringComparison.OrdinalIgnoreCase)
                   .Replace("<system>", "[system]", StringComparison.OrdinalIgnoreCase)
                   .Replace("</system>", "[/system]", StringComparison.OrdinalIgnoreCase)
                   .Replace("system:", "system -", StringComparison.OrdinalIgnoreCase)
                   .Replace("developer:", "developer -", StringComparison.OrdinalIgnoreCase)
                   .Replace("assistant:", "assistant -", StringComparison.OrdinalIgnoreCase);

        return text.Trim();
    }

    private static JsonDocument? ParseJsonArguments(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonDocument.Parse(json);
        }
        catch
        {
            return null;
        }
    }

    private static string? GetOptionalString(JsonElement? root, string propertyName)
    {
        if (root?.TryGetProperty(propertyName, out var prop) == true && prop.ValueKind == JsonValueKind.String)
        {
            return prop.GetString();
        }

        return null;
    }

    private static decimal? GetOptionalDecimal(JsonElement? root, string propertyName)
    {
        if (root?.TryGetProperty(propertyName, out var prop) == true && prop.TryGetDecimal(out var val))
        {
            return val;
        }

        return null;
    }

    private static double? GetOptionalDouble(JsonElement? root, string propertyName)
    {
        if (root?.TryGetProperty(propertyName, out var prop) == true && prop.TryGetDouble(out var val))
        {
            return val;
        }

        return null;
    }

    private static int? GetOptionalInt(JsonElement? root, string propertyName)
    {
        if (root?.TryGetProperty(propertyName, out var prop) == true && prop.TryGetInt32(out var val))
        {
            return val;
        }

        return null;
    }
}
