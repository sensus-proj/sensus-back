using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using Sensus.Application.Common.Interfaces;
using Sensus.Domain.Enums;
using Sensus.Infrastructure.Services;

namespace Sensus.Infrastructure.Ai.Routing;

/// <summary>
/// Clasificador determinista de complejidad para enrutar el turno inicial hacia el modelo Fast o Reasoning (FR-046-12).
/// Respeta la afinidad sticky para proteger el Prompt Caching entre turnos subsecuentes.
/// </summary>
public sealed class DeterministicModelRouter : IAiModelRouter
{
    private readonly OpenAiSettings _settings;

    private static readonly Regex ComplexSignalsRegex = new(
        @"(?i)\b(comparar?|optimizar?|combinar?|itinerario|paquete\s+completo|presupuesto\s+distribuido|plan(?:ear)?|diseñ(?:a|ar)|cena\s+para|varias\s+opciones|menú\s+completo|recomiéndame\s+varios|armar\s+un\s+combo)\b",
        RegexOptions.Compiled);

    public DeterministicModelRouter(IOptions<OpenAiSettings> settings)
    {
        _settings = settings.Value;
    }

    public AiRoutingDecision RouteInitialTurn(string prompt, bool allowReasoning = true)
    {
        var fastModel = !string.IsNullOrWhiteSpace(_settings.Sensi.FastModel)
            ? _settings.Sensi.FastModel
            : "gpt-4o-mini";

        var reasoningModel = !string.IsNullOrWhiteSpace(_settings.Sensi.ReasoningModel)
            ? _settings.Sensi.ReasoningModel
            : "gpt-4o";

        if (string.IsNullOrWhiteSpace(prompt))
        {
            return new AiRoutingDecision(fastModel, AiRoutingTier.Fast, "Prompt vacío o inicial.");
        }

        var trimmed = prompt.Trim();

        if (!allowReasoning)
        {
            return new AiRoutingDecision(
                fastModel,
                AiRoutingTier.Fast,
                "Sub-techo de razonamiento agotado (3 USD); enrutado a modelo rápido.");
        }

        // Criterio 1: Palabras clave explícitas de razonamiento u optimización multi-paso
        if (ComplexSignalsRegex.IsMatch(trimmed))
        {
            return new AiRoutingDecision(
                reasoningModel,
                AiRoutingTier.Reasoning,
                "Consulta con señales de optimización o razonamiento multi-paso detectadas.");
        }

        // Criterio 2: Prompt extenso con múltiples oraciones y números/presupuestos complejos
        if (trimmed.Length > 250 && trimmed.Contains('$', StringComparison.Ordinal) && trimmed.Contains(" y ", StringComparison.OrdinalIgnoreCase))
        {
            return new AiRoutingDecision(
                reasoningModel,
                AiRoutingTier.Reasoning,
                "Consulta extensa con múltiples restricciones de presupuesto.");
        }

        return new AiRoutingDecision(
            fastModel,
            AiRoutingTier.Fast,
            "Consulta estándar enrutada a modelo rápido.");
    }
}
