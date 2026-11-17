using System.Security.Cryptography;
using System.Text;

namespace Sensus.Domain.Common;

/// <summary>
/// Generador determinístico de códigos de invitación de amigo (SNS-XXXXXX) alineado con la app móvil.
/// </summary>
public static class FriendInviteCodeGenerator
{
    public static string Generate(string? email, string? displayName = null)
    {
        var seed = !string.IsNullOrWhiteSpace(email)
            ? email.Trim().ToLowerInvariant()
            : (!string.IsNullOrWhiteSpace(displayName) ? displayName.Trim().ToLowerInvariant() : null);

        if (seed is null)
        {
            return "SNS-VECINO";
        }

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(seed));
        return $"SNS-{Convert.ToHexString(hash.AsSpan(0, 3))}";
    }
}
