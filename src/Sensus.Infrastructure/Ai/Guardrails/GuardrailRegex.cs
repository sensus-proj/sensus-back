namespace Sensus.Infrastructure.Ai.Guardrails;

/// <summary>
/// Límite de ejecución común para las expresiones regulares de Sensi IA (mitigación de ReDoS, regla S6444).
/// Ante <see cref="System.Text.RegularExpressions.RegexMatchTimeoutException"/> cada guardrail falla cerrado
/// (FR-046-09, FR-046-10, NFR-046-05).
/// </summary>
internal static class GuardrailRegex
{
    /// <summary>
    /// 1 s: corta cualquier retroceso catastrófico sin provocar falsos positivos por arranque en frío de Lambda
    /// o compilación de la expresión bajo carga (con 100 ms se bloqueaban entradas benignas).
    /// </summary>
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(1);
}
