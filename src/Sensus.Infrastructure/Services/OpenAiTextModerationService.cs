using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Common.Settings;

namespace Sensus.Infrastructure.Services;

public sealed class OpenAiTextModerationService : ITextModerationService
{
    public const string PolicyViolationMessage = "El contenido textual no cumple con las políticas de seguridad.";
    public const string QuarantineMessage = "El contenido ha sido puesto en cuarentena para revisión debido a fallas o sospechas en la moderación.";

    private static readonly JsonSerializerOptions CaseInsensitiveJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly OpenAiSettings _openAiSettings;
    private readonly SensiSettings _sensiSettings;
    private readonly ILogger<OpenAiTextModerationService> _logger;

    public OpenAiTextModerationService(
        HttpClient httpClient,
        IOptions<OpenAiSettings> openAiOptions,
        IOptions<SensiSettings> sensiOptions,
        ILogger<OpenAiTextModerationService> logger)
    {
        _httpClient = httpClient;
        _openAiSettings = openAiOptions.Value;
        _sensiSettings = sensiOptions.Value;
        _logger = logger;
    }

    public Task<TextModerationResult> ModerateTextAsync(
        string text,
        CancellationToken cancellationToken = default)
    {
        var model = !string.IsNullOrWhiteSpace(_sensiSettings.ModerationModel)
            ? _sensiSettings.ModerationModel.Trim()
            : "omni-moderation-latest";

        return ExecuteModerationAsync(text, contentContext: null, model, cancellationToken);
    }

    public Task<TextModerationResult> ModerateContentTextAsync(
        string text,
        string contentContext,
        CancellationToken cancellationToken = default)
    {
        var model = !string.IsNullOrWhiteSpace(_openAiSettings.Model)
            ? _openAiSettings.Model.Trim()
            : (Environment.GetEnvironmentVariable("OpenAiSettings__Model")?.Trim() ?? "omni-moderation-latest");

        return ExecuteModerationAsync(text, contentContext, model, cancellationToken);
    }

    private async Task<TextModerationResult> ExecuteModerationAsync(
        string text,
        string? contentContext,
        string model,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return new TextModerationResult(true, "Texto vacío aprobado.", [], "Approved");
        }

        var resolvedApiKey = ResolveApiKey();
        if (string.IsNullOrWhiteSpace(resolvedApiKey))
        {
            _logger.LogWarning("Clave de API de OpenAI no configurada para moderación de texto. Fail-closed activado (Quarantine).");
            return new TextModerationResult(false, QuarantineMessage, null, "Quarantine");
        }

        try
        {
            var baseUrl = string.IsNullOrWhiteSpace(_openAiSettings.BaseUrl)
                ? "https://api.openai.com/v1"
                : _openAiSettings.BaseUrl.TrimEnd('/');

            var timeoutSeconds = _sensiSettings.TimeoutSeconds > 0
                ? _sensiSettings.TimeoutSeconds
                : (_openAiSettings.TimeoutSeconds > 0 ? _openAiSettings.TimeoutSeconds : 15);

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

            if (model.StartsWith("gpt-", StringComparison.OrdinalIgnoreCase))
            {
                return await ModerateWithChatModelAsync(
                    text,
                    contentContext,
                    resolvedApiKey,
                    baseUrl,
                    model,
                    cts.Token);
            }

            return await ModerateWithOmniModerationAsync(
                text,
                resolvedApiKey,
                baseUrl,
                model,
                cts.Token);
        }
        catch (OperationCanceledException)
        {
            _logger.LogError("Timeout al conectar con el servicio de OpenAI Moderation de texto.");
            return new TextModerationResult(false, QuarantineMessage, null, "Quarantine");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Excepción durante la moderación de texto con OpenAI.");
            return new TextModerationResult(false, QuarantineMessage, null, "Quarantine");
        }
    }

    private string? ResolveApiKey()
    {
        return !string.IsNullOrWhiteSpace(_openAiSettings.ApiKey)
            ? _openAiSettings.ApiKey.Trim()
            : (Environment.GetEnvironmentVariable("OPENAI_API_KEY")?.Trim()
               ?? Environment.GetEnvironmentVariable("OpenAiSettings__ApiKey")?.Trim());
    }

    private async Task<TextModerationResult> ModerateWithChatModelAsync(
        string text,
        string? contentContext,
        string resolvedApiKey,
        string baseUrl,
        string model,
        CancellationToken cancellationToken)
    {
        const string systemPrompt = @"Eres el sistema de moderación estricta de texto de Sensus (marketplace local de productos y comercios). Evalúa si el texto proporcionado es apto o contiene lenguaje prohibido.

PROHIBICIONES ESTRICTAS:
1. PALABRAS ANTISONANTES, GROSERÍAS O VULGARIDADES: Cualquier grosería, insulto, maldición, albur explícito, palabra obscena o vulgar en español o inglés (incluyendo variantes disfrazadas con símbolos, números o espacios como p*to, p3nd3j0, v3rg4, ch1ng4, etc.).
2. NOMBRES OFENSIVOS: Nombres de usuario, comercios o productos diseñados para ofender, insultar, acosar o resultar vulgares.
3. ODIO Y DISCRIMINACIÓN: Racismo, xenofobia, homofobia, machismo agresivo, simbología nazi o extremista.
4. VIOLENCIA, ARMAS O DROGAS ILÍCITAS: Amenazas, violencia explícita, armas de fuego/blancas ofensivas, estupefacientes o sustancias ilegales.
5. CONTENIDO SEXUAL EXPLÍCITO: Pornografía o insinuaciones sexuales explícitas.

REGLAS DE CLASIFICACIÓN:
- Si detectas palabras antisonantes, groserías, insultos, odio, armas, drogas o contenido sexual -> 'decision': 'Rejected'.
- Si es ambiguo o sospechoso de doble sentido ofensivo -> 'decision': 'Quarantine'.
- Si es un nombre de persona normal, nombre comercial legítimo o producto comercial válido -> 'decision': 'Approved'.

Responde OBLIGATORIAMENTE un JSON válido con esta estructura exacta:
{
  ""decision"": ""Approved"" | ""Quarantine"" | ""Rejected"",
  ""reason"": ""Explicación breve y clara en español para el usuario"",
  ""flagged_categories"": [""profanity"", ""harassment"", ""hate"", ""weapons"", ""drugs"", ""violence"", ""sexual""]
}";

        var contextLabel = string.IsNullOrWhiteSpace(contentContext) ? "Texto de plataforma" : contentContext.Trim();
        var requestPayload = new
        {
            model,
            messages = new object[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = $"Contexto: {contextLabel}\nTexto a evaluar: {text.Trim()}" }
            },
            response_format = new { type = "json_object" },
            max_tokens = 250,
            temperature = 0.0
        };

        var requestUri = new Uri($"{baseUrl}/chat/completions");
        using var request = new HttpRequestMessage(HttpMethod.Post, requestUri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", resolvedApiKey);
        request.Content = JsonContent.Create(requestPayload);

        var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Error al invocar OpenAI Chat Completions para moderación de texto (HTTP {StatusCode}): {ErrorBody}", response.StatusCode, errorBody);
            return new TextModerationResult(false, QuarantineMessage, null, "Quarantine");
        }

        var chatResponse = await response.Content.ReadFromJsonAsync<OpenAiChatModerationResponse>(cancellationToken: cancellationToken);
        var content = chatResponse?.Choices?.FirstOrDefault()?.Message?.Content;
        if (string.IsNullOrWhiteSpace(content))
        {
            _logger.LogWarning("OpenAI Chat Completions devolvió contenido vacío en moderación de texto. Fail-closed activado.");
            return new TextModerationResult(false, QuarantineMessage, null, "Quarantine");
        }

        ChatTextModerationPayload? parsed;
        try
        {
            parsed = JsonSerializer.Deserialize<ChatTextModerationPayload>(content, CaseInsensitiveJsonOptions);
        }
        catch (JsonException)
        {
            _logger.LogWarning("JSON inválido en moderación de texto con modelo {Model}. Fail-closed activado.", model);
            return new TextModerationResult(false, QuarantineMessage, null, "Quarantine");
        }

        if (parsed is null)
        {
            return new TextModerationResult(false, QuarantineMessage, null, "Quarantine");
        }

        var decision = parsed.Decision?.Trim().ToLowerInvariant() switch
        {
            "approved" => "Approved",
            "quarantine" => "Quarantine",
            _ => "Rejected"
        };

        var flagged = parsed.FlaggedCategories ?? [];
        if (decision == "Approved")
        {
            return new TextModerationResult(true, "Texto aprobado.", [], "Approved");
        }

        if (decision == "Quarantine")
        {
            var msg = !string.IsNullOrWhiteSpace(parsed.Reason) ? parsed.Reason : QuarantineMessage;
            _logger.LogWarning("Texto en cuarentena por moderación ({Model}). Categorías: {Categories}", model, string.Join(", ", flagged));
            return new TextModerationResult(false, msg, flagged, "Quarantine");
        }

        var rejectedMsg = !string.IsNullOrWhiteSpace(parsed.Reason) ? parsed.Reason : PolicyViolationMessage;
        _logger.LogWarning("Texto rechazado por moderación ({Model}). Categorías: {Categories}", model, string.Join(", ", flagged));
        return new TextModerationResult(false, rejectedMsg, flagged, "Rejected");
    }

    private async Task<TextModerationResult> ModerateWithOmniModerationAsync(
        string text,
        string resolvedApiKey,
        string baseUrl,
        string model,
        CancellationToken cancellationToken)
    {
        var requestPayload = new
        {
            model,
            input = text
        };

        var requestUri = new Uri($"{baseUrl}/moderations");
        using var request = new HttpRequestMessage(HttpMethod.Post, requestUri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", resolvedApiKey);
        request.Content = JsonContent.Create(requestPayload);

        var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Error al invocar OpenAI Moderations de texto (HTTP {StatusCode}): {ErrorBody}", response.StatusCode, errorBody);
            return new TextModerationResult(false, QuarantineMessage, null, "Quarantine");
        }

        var responseJson = await response.Content.ReadFromJsonAsync<OpenAiTextModerationResponse>(cancellationToken: cancellationToken);
        var firstResult = responseJson?.Results?.FirstOrDefault();

        if (firstResult is null)
        {
            _logger.LogWarning("OpenAI Moderations no devolvió resultados para la entrada de texto. Fail-closed activado.");
            return new TextModerationResult(false, QuarantineMessage, null, "Quarantine");
        }

        var categoryScores = firstResult.CategoryScores ?? new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        var categories = firstResult.Categories ?? new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

        var flaggedCategories = new List<string>();
        var quarantineCategories = new List<string>();

        foreach (var (category, score) in categoryScores)
        {
            var threshold = GetThresholdForCategory(category);
            if (score >= threshold.High)
            {
                flaggedCategories.Add(category);
            }
            else if (score >= threshold.Low)
            {
                quarantineCategories.Add(category);
            }
        }

        if (firstResult.Flagged)
        {
            foreach (var (cat, isFlagged) in categories)
            {
                if (isFlagged && !flaggedCategories.Contains(cat, StringComparer.OrdinalIgnoreCase))
                {
                    flaggedCategories.Add(cat);
                }
            }
        }

        if (flaggedCategories.Count > 0 || firstResult.Flagged)
        {
            _logger.LogWarning("Texto rechazado por moderación. Categorías marcadas: {Categories}", string.Join(", ", flaggedCategories));
            return new TextModerationResult(
                IsApproved: false,
                Message: PolicyViolationMessage,
                FlaggedCategories: flaggedCategories,
                Decision: "Rejected",
                CategoryScores: categoryScores);
        }

        if (quarantineCategories.Count > 0)
        {
            _logger.LogWarning("Texto en cuarentena por puntuaciones intermedias. Categorías: {Categories}", string.Join(", ", quarantineCategories));
            return new TextModerationResult(
                IsApproved: false,
                Message: QuarantineMessage,
                FlaggedCategories: quarantineCategories,
                Decision: "Quarantine",
                CategoryScores: categoryScores);
        }

        return new TextModerationResult(
            IsApproved: true,
            Message: "Texto aprobado.",
            FlaggedCategories: [],
            Decision: "Approved",
            CategoryScores: categoryScores);
    }

    private CategoryThreshold GetThresholdForCategory(string category)
    {
        if (_openAiSettings.Sensi?.TextModerationThresholds != null &&
            _openAiSettings.Sensi.TextModerationThresholds.TryGetValue(category, out var sensiThreshold) &&
            sensiThreshold != null)
        {
            return new CategoryThreshold
            {
                Low = sensiThreshold.Low,
                High = sensiThreshold.High
            };
        }

        return new CategoryThreshold
        {
            Low = _openAiSettings.DefaultLowThreshold > 0 ? _openAiSettings.DefaultLowThreshold : 0.35,
            High = _openAiSettings.DefaultHighThreshold > 0 ? _openAiSettings.DefaultHighThreshold : 0.75
        };
    }

    private sealed class OpenAiTextModerationResponse
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("model")]
        public string? Model { get; set; }

        [JsonPropertyName("results")]
        public List<OpenAiTextModerationItem>? Results { get; set; }
    }

    private sealed class OpenAiTextModerationItem
    {
        [JsonPropertyName("flagged")]
        public bool Flagged { get; set; }

        [JsonPropertyName("categories")]
        public Dictionary<string, bool>? Categories { get; set; }

        [JsonPropertyName("category_scores")]
        public Dictionary<string, double>? CategoryScores { get; set; }
    }

    private sealed class OpenAiChatModerationResponse
    {
        [JsonPropertyName("choices")]
        public List<OpenAiChatModerationChoice>? Choices { get; set; }
    }

    private sealed class OpenAiChatModerationChoice
    {
        [JsonPropertyName("message")]
        public OpenAiChatModerationMessage? Message { get; set; }
    }

    private sealed class OpenAiChatModerationMessage
    {
        [JsonPropertyName("content")]
        public string? Content { get; set; }
    }

    private sealed class ChatTextModerationPayload
    {
        [JsonPropertyName("decision")]
        public string? Decision { get; set; }

        [JsonPropertyName("reason")]
        public string? Reason { get; set; }

        [JsonPropertyName("flagged_categories")]
        public List<string>? FlaggedCategories { get; set; }
    }
}
