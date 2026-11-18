using System.Security.Claims;
using Sensus.Application.Common.Interfaces;
using Sensus.Domain.Enums;

namespace Sensus.Infrastructure.Services;

/// <summary>
/// Mapea los roles del ClaimsPrincipal verificado por JWT a los alcances (scopes) agénticos de Sensi IA (FR-046-02).
/// Principio de mínimo privilegio: los invitados no tienen acceso a Sensi IA (retorna conjunto vacío).
/// </summary>
public sealed class JwtScopeAuthorizationMapper : IScopeAuthorizationMapper
{
    private static readonly HashSet<AiScope> ConsumerScopes = new()
    {
        AiScope.AiChat,
        AiScope.OffersRead,
        AiScope.AccountRead,
        AiScope.ClaimsRead
    };

    private static readonly HashSet<AiScope> MerchantScopes = new()
    {
        AiScope.AiChat,
        AiScope.OffersRead,
        AiScope.AccountRead,
        AiScope.ClaimsRead,
        AiScope.MerchantRead
    };

    public IReadOnlySet<AiScope> MapScopes(ClaimsPrincipal user)
    {
        if (user?.Identity?.IsAuthenticated != true)
        {
            return new HashSet<AiScope>();
        }

        if (user.IsInRole(RoleType.Invitado))
        {
            return new HashSet<AiScope>();
        }

        if (user.IsInRole(RoleType.Comerciante) || user.IsInRole(RoleType.Administrador))
        {
            return MerchantScopes;
        }

        if (user.IsInRole(RoleType.Consumidor))
        {
            return ConsumerScopes;
        }

        // Por defecto para cualquier otro usuario autenticado estándar
        return ConsumerScopes;
    }

    public IReadOnlySet<AiScope> MapScopesByRole(string? role)
    {
        if (string.IsNullOrWhiteSpace(role) || string.Equals(role, RoleType.Invitado, StringComparison.OrdinalIgnoreCase))
        {
            return new HashSet<AiScope>();
        }

        if (string.Equals(role, RoleType.Comerciante, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(role, RoleType.Administrador, StringComparison.OrdinalIgnoreCase))
        {
            return MerchantScopes;
        }

        return ConsumerScopes;
    }
}
