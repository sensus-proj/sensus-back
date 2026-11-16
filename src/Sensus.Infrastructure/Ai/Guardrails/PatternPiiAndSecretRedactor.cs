using System.Text.RegularExpressions;
using Sensus.Application.Common.Interfaces;

namespace Sensus.Infrastructure.Ai.Guardrails;

/// <summary>
/// Redactor de PII y secretos con patrones deterministas para salida y trazas persistidas (FR-046-10, FR-046-15, NFR-046-06).
/// Cumple con la Ley Federal de Protección de Datos Personales en Posesión de los Particulares (LFPDPPP).
/// </summary>
public sealed class PatternPiiAndSecretRedactor : IPiiRedactor
{
    private static readonly Regex EmailPattern = new(
        @"[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}",
        RegexOptions.Compiled);

    private static readonly Regex PhonePattern = new(
        @"(?:(?:\+?52\s*)?(?:\(?\d{2,3}\)?[\s.-]?)?\d{4}[\s.-]?\d{4}|\b\d{10}\b)",
        RegexOptions.Compiled);

    private static readonly Regex CreditCardPattern = new(
        @"\b(?:\d{4}[-\s]?){3}\d{4}\b|\b\d{15,16}\b",
        RegexOptions.Compiled);

    private static readonly Regex CurpPattern = new(
        @"\b[A-Z]{4}\d{6}[HM][A-Z]{5}[A-Z0-9]\d\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex RfcPattern = new(
        @"\b[A-Z&Ñ]{3,4}\d{6}[A-Z0-9]{3}\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex JwtSecretPattern = new(
        @"\beyJ[a-zA-Z0-9_-]+\.[a-zA-Z0-9_-]+\.[a-zA-Z0-9_-]+\b",
        RegexOptions.Compiled);

    private static readonly Regex OpenAiApiKeyPattern = new(
        @"\bsk-[a-zA-Z0-9_-]{20,}\b",
        RegexOptions.Compiled);

    public string Redact(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return string.Empty;
        }

        var result = JwtSecretPattern.Replace(input, "[TOKEN_REDACTED]");
        result = OpenAiApiKeyPattern.Replace(result, "[API_KEY_REDACTED]");
        result = EmailPattern.Replace(result, "[EMAIL_REDACTED]");
        result = CreditCardPattern.Replace(result, "[CARD_REDACTED]");
        result = CurpPattern.Replace(result, "[CURP_REDACTED]");
        result = RfcPattern.Replace(result, "[RFC_REDACTED]");
        result = PhonePattern.Replace(result, "[PHONE_REDACTED]");

        return result;
    }
}
