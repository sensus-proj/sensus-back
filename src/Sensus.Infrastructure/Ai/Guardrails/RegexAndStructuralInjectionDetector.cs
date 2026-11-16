using System.Text.RegularExpressions;
using Sensus.Application.Common.Interfaces;

namespace Sensus.Infrastructure.Ai.Guardrails;

/// <summary>
/// Detector determinista anti-Prompt Injection y Jailbreak basado en análisis estructural y patrones heurísticos (FR-046-04, FR-046-09).
/// </summary>
public sealed class RegexAndStructuralInjectionDetector : IPromptInjectionDetector
{
    private static readonly Regex[] InjectionPatterns = new[]
    {
        // Instrucciones explícitas de evasión o cambio de rol
        new Regex(@"(?i)\b(ignore|ignora|olvida|deshazte|bypas(?:s|se)|override)\s+(all|todas|the|las|previous|anteriores)?\s*(instructions|instrucciones|directivas|reglas)\b", RegexOptions.Compiled, GuardrailRegex.Timeout),
        new Regex(@"(?i)\b(you are now|ahora eres|act as|actúa como|simula ser)\s+(an?\s+unfiltered|dan|unrestricted|sin restricciones)\b", RegexOptions.Compiled, GuardrailRegex.Timeout),
        new Regex(@"(?i)\b(dan mode|developer mode|modo desarrollador|jailbreak)\b", RegexOptions.Compiled, GuardrailRegex.Timeout),
        new Regex(@"(?i)\b(reveal|revela|muestra|print|imprime|dime)\s+(tu\s+prompt|your\s+system\s+prompt|las\s+instrucciones\s+del\s+sistema|the\s+system\s+instructions)\b", RegexOptions.Compiled, GuardrailRegex.Timeout),

        // Inyecciones estructurales de delimitadores del modelo
        new Regex(@"<\/?(contexto|system|developer|assistant|instruction|prompt)[^>]*>", RegexOptions.Compiled | RegexOptions.IgnoreCase, GuardrailRegex.Timeout),
        new Regex(@"(?i)(role\s*:\s*(system|developer|assistant)|developer\s*:|system\s*:)", RegexOptions.Compiled, GuardrailRegex.Timeout),

        // Manipulación de formato especial de ChatML o OpenAI
        new Regex(@"<\|im_start\|>|<\|im_end\|>|<\|endoftext\|>", RegexOptions.Compiled | RegexOptions.IgnoreCase, GuardrailRegex.Timeout)
    };

    public PromptInjectionCheckResult Check(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return new PromptInjectionCheckResult(false);
        }

        try
        {
            foreach (var pattern in InjectionPatterns)
            {
                if (pattern.IsMatch(input))
                {
                    return new PromptInjectionCheckResult(
                        true,
                        "Patrón de inyección de instrucciones o manipulación estructural detectado en la entrada.");
                }
            }
        }
        catch (RegexMatchTimeoutException)
        {
            // Fail-closed: una entrada que agota el tiempo de análisis se trata como sospechosa.
            return new PromptInjectionCheckResult(true, "Tiempo de análisis de la entrada agotado.");
        }

        return new PromptInjectionCheckResult(false);
    }
}
