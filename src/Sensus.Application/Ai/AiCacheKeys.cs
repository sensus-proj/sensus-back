using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Sensus.Application.Ai;

/// <summary>
/// Claves e invariantes de normalización para la caché distribuida de respuestas FAQ de Sensi IA (Spec 046 FR-046-13, Spec 048).
/// </summary>
public static class AiCacheKeys
{
    /// <summary>Nombre de entidad para resolver el TTL desde <c>Redis:TtlByEntityMinutes</c> (FR-048-10).</summary>
    public const string FaqEntityName = "AiFaq";

    public const string FaqPrefix = "sensus:ai:faq:";

    /// <summary>
    /// Normaliza una pregunta frecuente sin expresiones regulares: minúsculas, sin tildes, conservando solo letras y
    /// dígitos y colapsando separadores en un único espacio.
    /// </summary>
    public static string NormalizePrompt(string? prompt)
    {
        if (string.IsNullOrWhiteSpace(prompt))
        {
            return string.Empty;
        }

        string decomposed = prompt.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        bool pendingSpace = false;

        foreach (char ch in decomposed)
        {
            UnicodeCategory category = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (category == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsLetterOrDigit(ch))
            {
                if (pendingSpace && builder.Length > 0)
                {
                    builder.Append(' ');
                }

                builder.Append(ch);
                pendingSpace = false;
            }
            else if (builder.Length > 0)
            {
                pendingSpace = true;
            }
        }

        return builder.ToString();
    }

    /// <summary>
    /// Construye la clave de caché usando un hash SHA-256 de la consulta normalizada para nunca exponer texto de usuario en Redis,
    /// aislada por versión Markdown, rol y contexto de pantalla.
    /// </summary>
    public static string Faq(string markdownVersion, string role, string prompt, string? screenContext = null)
    {
        string safeVersion = string.IsNullOrWhiteSpace(markdownVersion) ? "1.0.0" : markdownVersion.Trim();
        string safeRole = string.IsNullOrWhiteSpace(role) ? "unknown" : role.Trim().ToLowerInvariant();
        string safeScreen = string.IsNullOrWhiteSpace(screenContext) ? "none" : screenContext.Trim().ToLowerInvariant();
        string normalized = NormalizePrompt(prompt);
        string hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(normalized)));

        return $"{FaqPrefix}{safeVersion}:{safeRole}:{safeScreen}:{hash}";
    }
}
