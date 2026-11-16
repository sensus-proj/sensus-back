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

    public async Task<TextModerationResult> ModerateTextAsync(
        string text,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return new TextModerationResult(true, "Texto vacío aprobado.", [], "Approved");
        }

        var resolvedApiKey = !string.IsNullOrWhiteSpace(_openAiSettings.ApiKey)
            ? _openAiSettings.ApiKey.Trim()
            : (Environment.GetEnvironmentVariable("OPENAI_API_KEY")?.Trim()
               ?? Environment.GetEnvironmentVariable("OpenAiSettings__ApiKey")?.Trim());

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

            var model = !string.IsNullOrWhiteSpace(_sensiSettings.ModerationModel)
                ? _sensiSettings.ModerationModel.Trim()
                : "omni-moderation-latest";

            var requestPayload = new
            {
                model = model,
                input = text
            };

            var requestUri = new Uri($"{baseUrl}/moderations");
            using var request = new HttpRequestMessage(HttpMethod.Post, requestUri);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", resolvedApiKey);
            request.Content = JsonContent.Create(requestPayload);

            var timeoutSeconds = _sensiSettings.TimeoutSeconds > 0
                ? _sensiSettings.TimeoutSeconds
                : (_openAiSettings.TimeoutSeconds > 0 ? _openAiSettings.TimeoutSeconds : 15);

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

            var response = await _httpClient.SendAsync(request, cts.Token);
            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cts.Token);
                _logger.LogError("Error al invocar OpenAI Moderations de texto (HTTP {StatusCode}): {ErrorBody}", response.StatusCode, errorBody);
                return new TextModerationResult(false, QuarantineMessage, null, "Quarantine");
            }

            var responseJson = await response.Content.ReadFromJsonAsync<OpenAiTextModerationResponse>(cancellationToken: cts.Token);
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
}
