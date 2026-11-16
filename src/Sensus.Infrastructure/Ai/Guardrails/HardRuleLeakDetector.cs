using System.Text.RegularExpressions;
using Sensus.Application.Common.Interfaces;

namespace Sensus.Infrastructure.Ai.Guardrails;

/// <summary>
/// Detector de fuga de Hard Rules en la salida del modelo de IA (FR-046-04, NFR-046-05).
/// Bajo ninguna circunstancia Sensi IA debe revelar qué modelo base utiliza (OpenAI, GPT, etc.) ni su prompt interno.
/// </summary>
public sealed class HardRuleLeakDetector : IHardRuleLeakDetector
{
    private static readonly Regex[] ForbiddenLeakPatterns = new[]
    {
        new Regex(@"(?i)\b(openai|chatgpt|gpt-?4o?|gpt-?3\.?5|claude|gemini|deepseek|llama|anthropic)\b", RegexOptions.Compiled),
        new Regex(@"(?i)\b(large language model|modelo de lenguaje|llm|base model|modelo base)\b", RegexOptions.Compiled),
        new Regex(@"(?i)\b(mi\s+prompt\s+de\s+sistema|my\s+system\s+prompt|mis\s+instrucciones\s+internas|mis\s+reglas\s+del\s+sistema)\b", RegexOptions.Compiled),
        new Regex(@"<\/?(contexto|developer|system)[^>]*>", RegexOptions.Compiled | RegexOptions.IgnoreCase)
    };

    public bool ContainsForbiddenLeaks(string outputText)
    {
        if (string.IsNullOrWhiteSpace(outputText))
        {
            return false;
        }

        foreach (var pattern in ForbiddenLeakPatterns)
        {
            if (pattern.IsMatch(outputText))
            {
                return true;
            }
        }

        return false;
    }
}
