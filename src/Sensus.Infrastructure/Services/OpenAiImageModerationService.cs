using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sensus.Application.Common.Interfaces;

namespace Sensus.Infrastructure.Services;

public sealed class OpenAiImageModerationService : IImageModerationService
{
    public const string PolicyViolationMessage = "No se pudo subir la imagen por políticas de seguridad.";
    public const string QuarantineMessage = "El contenido ha sido puesto en cuarentena para revisión debido a puntuaciones sospechosas de seguridad.";

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly HttpClient _httpClient;
    private readonly OpenAiSettings _settings;
    private readonly ILogger<OpenAiImageModerationService> _logger;

    public OpenAiImageModerationService(
        HttpClient httpClient,
        IOptions<OpenAiSettings> options,
        ILogger<OpenAiImageModerationService> logger)
    {
        _httpClient = httpClient;
        _settings = options.Value;
        _logger = logger;
    }

    public async Task<ImageModerationResult> ModerateImageBytesAsync(
        byte[] imageBytes,
        string contentType = "image/jpeg",
        string? textContext = null,
        CancellationToken cancellationToken = default)
    {
        if (imageBytes is null || imageBytes.Length == 0)
        {
            return new ImageModerationResult(false, "La imagen no puede estar vacía.", null, "Rejected");
        }

        var resolvedApiKey = !string.IsNullOrWhiteSpace(_settings.ApiKey)
            ? _settings.ApiKey.Trim()
            : (Environment.GetEnvironmentVariable("OPENAI_API_KEY")?.Trim()
               ?? Environment.GetEnvironmentVariable("OpenAiSettings__ApiKey")?.Trim());

        if (string.IsNullOrWhiteSpace(resolvedApiKey))
        {
            _logger.LogWarning(
                "[MODO DESARROLLO / OPENAI MODERATION] Clave de API no configurada. Configure la variable de entorno 'OPENAI_API_KEY' (o 'OpenAiSettings__ApiKey' / 'OpenAiSettings:ApiKey' en appsettings.json) para moderación con OpenAI.");
            return new ImageModerationResult(true, "Moderación omitida en desarrollo local por falta de ApiKey.", null, "Approved");
        }

        try
        {
            var base64 = Convert.ToBase64String(imageBytes);
            var normalizedContentType = string.IsNullOrWhiteSpace(contentType) ? "image/jpeg" : contentType;
            var dataUrl = $"data:{normalizedContentType};base64,{base64}";

            var resolvedModel = !string.IsNullOrWhiteSpace(_settings.Model)
                ? _settings.Model.Trim()
                : (Environment.GetEnvironmentVariable("OpenAiSettings__Model")?.Trim() ?? "gpt-4o-mini");

            var baseUrl = string.IsNullOrWhiteSpace(_settings.BaseUrl)
                ? "https://api.openai.com/v1"
                : _settings.BaseUrl.TrimEnd('/');

            if (resolvedModel.StartsWith("gpt-", StringComparison.OrdinalIgnoreCase))
            {
                return await ModerateWithChatVisionAsync(resolvedApiKey, baseUrl, resolvedModel, dataUrl, textContext, cancellationToken);
            }

            return await ModerateWithOmniModerationAsync(resolvedApiKey, baseUrl, resolvedModel, dataUrl, textContext, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogError("Timeout al conectar con el servicio de OpenAI Moderation.");
            return new ImageModerationResult(false, PolicyViolationMessage, null, "Rejected");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Excepción no controlada durante la moderación de imagen con OpenAI.");
            return new ImageModerationResult(false, PolicyViolationMessage, null, "Rejected");
        }
    }

    private async Task<ImageModerationResult> ModerateWithChatVisionAsync(
        string resolvedApiKey,
        string baseUrl,
        string model,
        string dataUrl,
        string? textContext,
        CancellationToken cancellationToken)
    {
        const string systemPrompt = @"Eres el sistema de seguridad y moderación estricta de Sensus (marketplace comercial de productos y alimentos locales). Tu función es analizar visualmente la imagen y el texto adjunto para detectar contenido prohibido o peligroso.

PROHIBICIONES ESTRICTAS:
1. ARMAS DE FUEGO O BLANCAS: Pistolas, revólveres, rifles, escopetas, piezas de armas, cargadores, munición, cuchillos tácticos, dagas o machetes ofensivos.
2. SIMBOLOGÍA DE ODIO O NAZI: Esvásticas (cruces gamadas en cualquier ángulo o rotación), soles negros, runas SS, simbología fascista, insignias supremacistas o discriminatorias.
3. DROGAS O SUSTANCIAS ILÍCITAS: Estupefacientes, drogas ilegales, narcóticos o parafernalia de consumo.
4. CONTENIDO SEXUAL O DESNUDEZ: Pornografía, desnudez total o parcial explícita, actos sexuales.
5. VIOLENCIA O SANGRE: Violencia explícita, heridas graves, mutilaciones o muerte.

REGLAS DE CLASIFICACIÓN:
- Si detectas armas (de fuego o réplicas indistinguibles), munición, esvásticas nazis, drogas o desnudez -> 'decision': 'Rejected'.
- Si la imagen contiene un elemento sospechoso, ambiguo o potencialmente peligroso -> 'decision': 'Quarantine'.
- Si es un producto comercial normal, comida, oferta segura o inocua -> 'decision': 'Approved'.

Responde OBLIGATORIAMENTE un JSON válido con esta estructura exacta:
{
  ""decision"": ""Approved"" | ""Quarantine"" | ""Rejected"",
  ""reason"": ""Explicación breve en español del motivo"",
  ""flagged_categories"": [""weapons"", ""hate"", ""drugs"", ""violence"", ""sexual""],
  ""category_scores"": {
    ""weapons"": 0.0,
    ""hate"": 0.0,
    ""violence"": 0.0,
    ""sexual"": 0.0,
    ""illicit"": 0.0
  }
}";

        var userContentList = new List<object>();
        if (!string.IsNullOrWhiteSpace(textContext))
        {
            userContentList.Add(new
            {
                type = "text",
                text = $"Contexto de la publicación: {textContext.Trim()}"
            });
        }
        else
        {
            userContentList.Add(new
            {
                type = "text",
                text = "Evalúa la siguiente imagen para publicación en el catálogo:"
            });
        }

        userContentList.Add(new
        {
            type = "image_url",
            image_url = new
            {
                url = dataUrl,
                detail = "low"
            }
        });

        var requestPayload = new
        {
            model = model,
            messages = new object[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userContentList }
            },
            response_format = new { type = "json_object" },
            max_tokens = 300,
            temperature = 0.0
        };

        var requestUri = new Uri($"{baseUrl}/chat/completions");
        using var request = new HttpRequestMessage(HttpMethod.Post, requestUri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", resolvedApiKey);
        request.Content = JsonContent.Create(requestPayload);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(_settings.TimeoutSeconds > 0 ? _settings.TimeoutSeconds : 15));

        var response = await _httpClient.SendAsync(request, cts.Token);
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cts.Token);
            _logger.LogError("Error al invocar OpenAI Chat Completions (HTTP {StatusCode}): {ErrorBody}", response.StatusCode, errorBody);
            return new ImageModerationResult(false, PolicyViolationMessage, null, "Rejected");
        }

        var chatResponse = await response.Content.ReadFromJsonAsync<OpenAiChatCompletionResponse>(cancellationToken: cts.Token);
        var content = chatResponse?.Choices?.FirstOrDefault()?.Message?.Content;
        if (string.IsNullOrWhiteSpace(content))
        {
            _logger.LogError("Respuesta vacía de OpenAI Chat Completions para visión.");
            return new ImageModerationResult(false, PolicyViolationMessage, null, "Rejected");
        }

        var parsed = JsonSerializer.Deserialize<ChatVisionModerationResult>(content, JsonOptions);
        if (parsed == null)
        {
            _logger.LogError("No se pudo deserializar el JSON de moderación de visión de OpenAI.");
            return new ImageModerationResult(false, PolicyViolationMessage, null, "Rejected");
        }

        var decision = parsed.Decision?.Trim().ToLowerInvariant() switch
        {
            "approved" => "Approved",
            "quarantine" => "Quarantine",
            _ => "Rejected"
        };

        var flagged = parsed.FlaggedCategories ?? [];
        var scores = parsed.CategoryScores ?? new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

        if (decision == "Rejected")
        {
            var msg = !string.IsNullOrWhiteSpace(parsed.Reason) ? parsed.Reason : PolicyViolationMessage;
            _logger.LogWarning("Imagen rechazada por moderación de visión ({Model}). Motivo: {Reason}, Categorías: {Categories}", model, msg, string.Join(", ", flagged));
            return new ImageModerationResult(false, msg, flagged, "Rejected", scores);
        }

        if (decision == "Quarantine")
        {
            var msg = !string.IsNullOrWhiteSpace(parsed.Reason) ? parsed.Reason : QuarantineMessage;
            _logger.LogWarning("Imagen enviada a cuarentena por visión ({Model}). Motivo: {Reason}, Categorías: {Categories}", model, msg, string.Join(", ", flagged));
            return new ImageModerationResult(false, msg, flagged, "Quarantine", scores);
        }

        return new ImageModerationResult(true, "Imagen aprobada.", [], "Approved", scores);
    }

    private async Task<ImageModerationResult> ModerateWithOmniModerationAsync(
        string resolvedApiKey,
        string baseUrl,
        string model,
        string dataUrl,
        string? textContext,
        CancellationToken cancellationToken)
    {
        var inputList = new List<object>();

        if (!string.IsNullOrWhiteSpace(textContext))
        {
            inputList.Add(new
            {
                type = "text",
                text = textContext.Trim()
            });
        }

        inputList.Add(new
        {
            type = "image_url",
            image_url = new
            {
                url = dataUrl
            }
        });

        var requestPayload = new
        {
            model = model,
            input = inputList
        };

        var requestUri = new Uri($"{baseUrl}/moderations");

        using var request = new HttpRequestMessage(HttpMethod.Post, requestUri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", resolvedApiKey);
        request.Content = JsonContent.Create(requestPayload);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(_settings.TimeoutSeconds > 0 ? _settings.TimeoutSeconds : 15));

        var response = await _httpClient.SendAsync(request, cts.Token);
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cts.Token);
            _logger.LogError("Error al invocar OpenAI Moderations (HTTP {StatusCode}): {ErrorBody}", response.StatusCode, errorBody);
            return new ImageModerationResult(false, PolicyViolationMessage, null, "Rejected");
        }

        var responseJson = await response.Content.ReadFromJsonAsync<OpenAiModerationResponse>(cancellationToken: cts.Token);
        var firstResult = responseJson?.Results?.FirstOrDefault();

        if (firstResult is null)
        {
            _logger.LogWarning("OpenAI Moderations no devolvió resultados en el arreglo.");
            return new ImageModerationResult(true, "Sin resultados para evaluar.", null, "Approved");
        }

        var categoryScores = firstResult.CategoryScores ?? new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        var categories = firstResult.Categories ?? new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

        var flaggedCategories = new List<string>();
        var quarantineCategories = new List<string>();

        // Evaluación por bandas y umbrales (FR-018-01, FR-018-02)
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

        // Integración de categorías flagged directas de OpenAI
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

        // Decisión según bandas (Banda Alta -> Rechazo, Banda Intermedia -> Cuarentena, Banda Baja -> Aprobado)
        if (flaggedCategories.Count > 0 || firstResult.Flagged)
        {
            _logger.LogWarning("Contenido rechazado en banda alta. Categorías marcadas: {Categories}", string.Join(", ", flaggedCategories));
            return new ImageModerationResult(
                IsApproved: false,
                Message: PolicyViolationMessage,
                FlaggedCategories: flaggedCategories,
                Decision: "Rejected",
                CategoryScores: categoryScores);
        }

        if (quarantineCategories.Count > 0)
        {
            _logger.LogWarning("Contenido enviado a cuarentena (banda intermedia). Categorías sospechosas: {Categories}", string.Join(", ", quarantineCategories));
            return new ImageModerationResult(
                IsApproved: false,
                Message: QuarantineMessage,
                FlaggedCategories: quarantineCategories,
                Decision: "Quarantine",
                CategoryScores: categoryScores);
        }

        return new ImageModerationResult(
            IsApproved: true,
            Message: "Imagen aprobada.",
            FlaggedCategories: [],
            Decision: "Approved",
            CategoryScores: categoryScores);
    }

    private CategoryThreshold GetThresholdForCategory(string category)
    {
        if (_settings.CategoryThresholds != null &&
            _settings.CategoryThresholds.TryGetValue(category, out var threshold) &&
            threshold != null)
        {
            return threshold;
        }

        return new CategoryThreshold
        {
            Low = _settings.DefaultLowThreshold > 0 ? _settings.DefaultLowThreshold : 0.35,
            High = _settings.DefaultHighThreshold > 0 ? _settings.DefaultHighThreshold : 0.75
        };
    }

    private sealed class OpenAiChatCompletionResponse
    {
        [JsonPropertyName("choices")]
        public List<OpenAiChatChoice>? Choices { get; set; }
    }

    private sealed class OpenAiChatChoice
    {
        [JsonPropertyName("message")]
        public OpenAiChatMessage? Message { get; set; }
    }

    private sealed class OpenAiChatMessage
    {
        [JsonPropertyName("content")]
        public string? Content { get; set; }
    }

    private sealed class ChatVisionModerationResult
    {
        [JsonPropertyName("decision")]
        public string? Decision { get; set; }

        [JsonPropertyName("reason")]
        public string? Reason { get; set; }

        [JsonPropertyName("flagged_categories")]
        public List<string>? FlaggedCategories { get; set; }

        [JsonPropertyName("category_scores")]
        public Dictionary<string, double>? CategoryScores { get; set; }
    }

    private sealed class OpenAiModerationResponse
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("model")]
        public string? Model { get; set; }

        [JsonPropertyName("results")]
        public List<OpenAiModerationItem>? Results { get; set; }
    }

    private sealed class OpenAiModerationItem
    {
        [JsonPropertyName("flagged")]
        public bool Flagged { get; set; }

        [JsonPropertyName("categories")]
        public Dictionary<string, bool>? Categories { get; set; }

        [JsonPropertyName("category_scores")]
        public Dictionary<string, double>? CategoryScores { get; set; }
    }
}
