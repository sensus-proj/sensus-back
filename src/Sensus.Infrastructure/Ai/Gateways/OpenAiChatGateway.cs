using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sensus.Application.Common.Interfaces;
using Sensus.Infrastructure.Services;

namespace Sensus.Infrastructure.Ai.Gateways;

/// <summary>
/// Gateway de integración con OpenAI Chat Completions API para Sensi IA (FR-046-11, FR-046-16).
/// Aplica store: false para privacidad estricta y respeta la estructura de payload de 2 bloques para Prompt Caching.
/// </summary>
public sealed class OpenAiChatGateway : IAiChatGateway
{
    private readonly HttpClient _httpClient;
    private readonly OpenAiSettings _settings;
    private readonly ILogger<OpenAiChatGateway> _logger;

    public OpenAiChatGateway(
        HttpClient httpClient,
        IOptions<OpenAiSettings> settings,
        ILogger<OpenAiChatGateway> logger)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<AiChatCompletionResult> CompleteTurnAsync(
        AiChatRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_settings.ApiKey))
        {
            _logger.LogError("OpenAI API Key no configurada para Sensi IA.");
            throw new InvalidOperationException("OpenAI API Key no disponible para el servicio de chat.");
        }

        var payload = BuildOpenAiPayload(request);
        var jsonContent = JsonSerializer.Serialize(payload);

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "chat/completions")
        {
            Content = new StringContent(jsonContent, Encoding.UTF8, "application/json")
        };
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _settings.ApiKey);

        _logger.LogInformation("Enviando llamada a OpenAI Chat API ({Model}) con {ToolCount} herramientas.",
            request.Model, request.Tools.Count);

        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Error en OpenAI Chat API. Status: {StatusCode}, Body: {Body}",
                response.StatusCode, errorBody);
            throw new HttpRequestException($"OpenAI Chat API devolvió status {response.StatusCode}: {errorBody}");
        }

        var responseString = await response.Content.ReadAsStringAsync(cancellationToken);
        return ParseOpenAiResponse(responseString);
    }

    private Dictionary<string, object> BuildOpenAiPayload(AiChatRequest request)
    {
        var messages = new List<object>();

        // Bloque 1: Estático (Cacheable) - Developer / System prompt con identidad y contexto Markdown
        messages.Add(new
        {
            role = "developer",
            content = request.SystemAndContextPrefix
        });

        // Bloque 2: Dinámico (No cacheable) - Datos de sesión del usuario
        if (!string.IsNullOrWhiteSpace(request.DynamicSessionContext))
        {
            messages.Add(new
            {
                role = "developer",
                content = request.DynamicSessionContext
            });
        }

        // Historial reciente de la conversación
        foreach (var historyItem in request.RecentHistory)
        {
            messages.Add(new
            {
                role = historyItem.Role.Equals("assistant", StringComparison.OrdinalIgnoreCase) ? "assistant" : "user",
                content = historyItem.Content
            });
        }

        // Prompt actual del usuario
        messages.Add(new
        {
            role = "user",
            content = request.UserPrompt
        });

        // Herramientas MCP ordenadas alfabéticamente
        var tools = request.Tools
            .OrderBy(t => t.Name, StringComparer.Ordinal)
            .Select(t => new
            {
                type = "function",
                function = new
                {
                    name = t.Name,
                    description = t.Description,
                    parameters = t.ParametersSchema
                }
            })
            .ToList();

        var payloadDict = new Dictionary<string, object>
        {
            ["model"] = request.Model,
            ["messages"] = messages,
            ["store"] = false, // REGLA CRÍTICA DE PRIVACIDAD
            ["max_completion_tokens"] = _settings.Sensi.MaxCompletionTokens,
            // FR-046-19: temperatura explícita; nunca la predeterminada del proveedor.
            ["temperature"] = _settings.Sensi.Temperature
        };

        if (tools.Count > 0)
        {
            payloadDict["tools"] = tools;
        }

        return payloadDict;
    }

    private static AiChatCompletionResult ParseOpenAiResponse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var replyText = string.Empty;
        var toolCalls = new List<McpToolCall>();

        if (root.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
        {
            var firstChoice = choices[0];
            if (firstChoice.TryGetProperty("message", out var message))
            {
                if (message.TryGetProperty("content", out var contentProp) && contentProp.ValueKind == JsonValueKind.String)
                {
                    replyText = contentProp.GetString() ?? string.Empty;
                }

                if (message.TryGetProperty("tool_calls", out var toolCallsProp) && toolCallsProp.ValueKind == JsonValueKind.Array)
                {
                    foreach (var tc in toolCallsProp.EnumerateArray())
                    {
                        var id = tc.GetProperty("id").GetString() ?? string.Empty;
                        var function = tc.GetProperty("function");
                        var name = function.GetProperty("name").GetString() ?? string.Empty;
                        var arguments = function.GetProperty("arguments").GetString() ?? "{}";

                        toolCalls.Add(new McpToolCall(id, name, arguments));
                    }
                }
            }
        }

        int promptTokens = 0;
        int cachedTokens = 0;
        int completionTokens = 0;

        if (root.TryGetProperty("usage", out var usage))
        {
            if (usage.TryGetProperty("prompt_tokens", out var pt))
            {
                promptTokens = pt.GetInt32();
            }

            if (usage.TryGetProperty("completion_tokens", out var ct))
            {
                completionTokens = ct.GetInt32();
            }

            if (usage.TryGetProperty("prompt_tokens_details", out var ptd) &&
                ptd.TryGetProperty("cached_tokens", out var cached))
            {
                cachedTokens = cached.GetInt32();
            }
        }

        return new AiChatCompletionResult(
            replyText,
            toolCalls,
            promptTokens,
            cachedTokens,
            completionTokens,
            cachedTokens > 0);
    }
}
