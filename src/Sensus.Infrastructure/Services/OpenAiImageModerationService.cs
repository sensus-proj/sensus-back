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
        CancellationToken cancellationToken = default)
    {
        if (imageBytes is null || imageBytes.Length == 0)
        {
            return new ImageModerationResult(false, "La imagen no puede estar vacía.");
        }

        var resolvedApiKey = !string.IsNullOrWhiteSpace(_settings.ApiKey)
            ? _settings.ApiKey.Trim()
            : (Environment.GetEnvironmentVariable("OPENAI_API_KEY")?.Trim()
               ?? Environment.GetEnvironmentVariable("OpenAiSettings__ApiKey")?.Trim());

        if (string.IsNullOrWhiteSpace(resolvedApiKey))
        {
            _logger.LogWarning(
                "[MODO DESARROLLO / OPENAI MODERATION] Clave de API no configurada. Configure la variable de entorno 'OPENAI_API_KEY' (o 'OpenAiSettings__ApiKey' / 'OpenAiSettings:ApiKey' en appsettings.json) para moderación con omni-moderation-latest.");
            return new ImageModerationResult(true, "Moderación omitida en desarrollo local por falta de ApiKey.");
        }

        try
        {
            var base64 = Convert.ToBase64String(imageBytes);
            var normalizedContentType = string.IsNullOrWhiteSpace(contentType) ? "image/jpeg" : contentType;
            var dataUrl = $"data:{normalizedContentType};base64,{base64}";

            var requestPayload = new
            {
                model = string.IsNullOrWhiteSpace(_settings.Model) ? "omni-moderation-latest" : _settings.Model,
                input = new object[]
                {
                    new
                    {
                        type = "image_url",
                        image_url = new
                        {
                            url = dataUrl
                        }
                    }
                }
            };

            var baseUrl = string.IsNullOrWhiteSpace(_settings.BaseUrl)
                ? "https://api.openai.com/v1"
                : _settings.BaseUrl.TrimEnd('/');
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
                return new ImageModerationResult(false, PolicyViolationMessage);
            }

            var responseJson = await response.Content.ReadFromJsonAsync<OpenAiModerationResponse>(cancellationToken: cts.Token);
            var firstResult = responseJson?.Results?.FirstOrDefault();

            if (firstResult is null)
            {
                _logger.LogWarning("OpenAI Moderations no devolvió resultados en el arreglo.");
                return new ImageModerationResult(true);
            }

            if (firstResult.Flagged)
            {
                var flaggedCategories = firstResult.Categories?
                    .Where(kvp => kvp.Value)
                    .Select(kvp => kvp.Key)
                    .ToList() ?? [];

                _logger.LogWarning("Imagen rechazada por políticas de OpenAI. Categorías marcadas: {Categories}", string.Join(", ", flaggedCategories));
                return new ImageModerationResult(false, PolicyViolationMessage, flaggedCategories);
            }

            return new ImageModerationResult(true, "Imagen aprobada.");
        }
        catch (OperationCanceledException)
        {
            _logger.LogError("Timeout al conectar con el servicio de OpenAI Moderation.");
            return new ImageModerationResult(false, PolicyViolationMessage);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Excepción no controlada durante la moderación de imagen con OpenAI.");
            return new ImageModerationResult(false, PolicyViolationMessage);
        }
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
    }
}
