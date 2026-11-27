using System.Security.Cryptography;
using System.Text;

namespace Sensus.Application.Common.Moderation;

public static class ModeratedTextFingerprint
{
    public static string Create(string text) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text.Trim())));

    /// <summary>
    /// Genera las huellas SHA-256 válidas para enlazar una imagen aprobada con el título y
    /// descripción de la oferta, aceptando salto de línea canónico y separador <c>" - "</c>.
    /// </summary>
    public static IReadOnlyList<string> CreateOfferCandidates(string title, string description)
    {
        string trimmedTitle = title.Trim();
        string trimmedDescription = description.Trim();
        string[] contexts =
        [
            $"{trimmedTitle}\n{trimmedDescription}",
            $"{trimmedTitle} - {trimmedDescription}"
        ];

        return contexts
            .Select(Create)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }
}
