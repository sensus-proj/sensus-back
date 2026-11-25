using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Sensus.Application.Common.Interfaces;
using Sensus.Domain.Enums;
using Sensus.Infrastructure.Ai.Gateways;
using Sensus.Infrastructure.Services;
using Xunit;

namespace Sensus.IntegrationTests.Services;

/// <summary>
/// Gateway de OpenAI Chat Completions para Sensi IA con doble HTTP (FR-046-11, FR-046-16): payload de dos
/// bloques, <c>store: false</c>, herramientas en orden canónico, lectura de uso/caché y fallos HTTP.
/// </summary>
public sealed class OpenAiChatGatewayTests
{
    private sealed class CapturingHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }

        public string? RequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            RequestBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        }
    }

    private static (OpenAiChatGateway Gateway, CapturingHandler Handler) CreateGateway(
        HttpStatusCode status,
        string body,
        string apiKey = "test-key",
        double? temperature = null)
    {
        var handler = new CapturingHandler(status, body);
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://api.openai.test/v1/") };
        var openAiSettings = new OpenAiSettings { ApiKey = apiKey };
        if (temperature.HasValue)
        {
            openAiSettings.Sensi.Temperature = temperature.Value;
        }

        var settings = Options.Create(openAiSettings);
        return (new OpenAiChatGateway(client, settings, NullLogger<OpenAiChatGateway>.Instance), handler);
    }

    private static AiChatRequest BuildRequest(bool withTools = true) => new(
        "gpt-4o-mini",
        withTools
            ?
            [
                new McpToolDefinition("search_offers", "Busca ofertas", new { type = "object" }, AiScope.OffersRead),
                new McpToolDefinition("get_my_claims", "Mis apartados", new { type = "object" }, AiScope.ClaimsRead)
            ]
            : [],
        "BLOQUE ESTATICO",
        "[SESIÓN_ACTIVA: Rol=Consumidor]",
        [new AiMessageHistoryItem("assistant", "Hola"), new AiMessageHistoryItem("user", "Busco tortas")],
        "¿Qué hay cerca?");

    [Fact]
    public async Task CompleteTurnAsync_ShouldThrow_WhenApiKeyIsMissing()
    {
        var (gateway, handler) = CreateGateway(HttpStatusCode.OK, "{}", apiKey: " ");

        var act = () => gateway.CompleteTurnAsync(BuildRequest());

        await act.Should().ThrowAsync<InvalidOperationException>();
        handler.Request.Should().BeNull();
    }

    [Fact]
    public async Task CompleteTurnAsync_ShouldSendTwoBlockPayload_WithStoreFalse_AndOrderedTools()
    {
        const string body = "{\"choices\":[{\"message\":{\"content\":\"Hola, soy Sensi\"}}]," +
            "\"usage\":{\"prompt_tokens\":1500,\"completion_tokens\":40,\"prompt_tokens_details\":{\"cached_tokens\":1024}}}";
        var (gateway, handler) = CreateGateway(HttpStatusCode.OK, body);

        var result = await gateway.CompleteTurnAsync(BuildRequest());

        result.ReplyText.Should().Be("Hola, soy Sensi");
        result.ToolCalls.Should().BeEmpty();
        result.PromptTokens.Should().Be(1500);
        result.CachedTokens.Should().Be(1024);
        result.CompletionTokens.Should().Be(40);
        result.CacheHit.Should().BeTrue();

        handler.Request!.Method.Should().Be(HttpMethod.Post);
        handler.Request.RequestUri!.AbsoluteUri.Should().Be("https://api.openai.test/v1/chat/completions");
        handler.Request.Headers.Authorization!.Scheme.Should().Be("Bearer");
        handler.Request.Headers.Authorization.Parameter.Should().Be("test-key");

        using JsonDocument payload = JsonDocument.Parse(handler.RequestBody!);
        JsonElement root = payload.RootElement;
        root.GetProperty("model").GetString().Should().Be("gpt-4o-mini");
        root.GetProperty("store").GetBoolean().Should().BeFalse();
        root.GetProperty("max_completion_tokens").GetInt32().Should().BeGreaterThan(0);

        JsonElement[] messages = [.. root.GetProperty("messages").EnumerateArray()];
        messages.Select(m => m.GetProperty("role").GetString()).Should()
            .Equal("developer", "developer", "assistant", "user", "user");
        messages[0].GetProperty("content").GetString().Should().Be("BLOQUE ESTATICO");
        messages[^1].GetProperty("content").GetString().Should().Be("¿Qué hay cerca?");

        root.GetProperty("tools").EnumerateArray()
            .Select(t => t.GetProperty("function").GetProperty("name").GetString())
            .Should().Equal("get_my_claims", "search_offers");
    }

    [Fact]
    public async Task CompleteTurnAsync_ShouldSendDefaultTemperatureZero_WhenNotConfigured()
    {
        // FR-046-19: la temperatura siempre viaja explícita; sin configuración es 0.0.
        var (gateway, handler) = CreateGateway(HttpStatusCode.OK, "{\"choices\":[]}");

        await gateway.CompleteTurnAsync(BuildRequest(withTools: false));

        using JsonDocument payload = JsonDocument.Parse(handler.RequestBody!);
        payload.RootElement.GetProperty("temperature").GetDouble().Should().Be(0.0);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(0.4)]
    [InlineData(1.0)]
    public async Task CompleteTurnAsync_ShouldSendConfiguredTemperature(double temperature)
    {
        var (gateway, handler) = CreateGateway(HttpStatusCode.OK, "{\"choices\":[]}", temperature: temperature);

        await gateway.CompleteTurnAsync(BuildRequest(withTools: false));

        using JsonDocument payload = JsonDocument.Parse(handler.RequestBody!);
        payload.RootElement.GetProperty("temperature").GetDouble().Should().Be(temperature);
    }

    [Fact]
    public async Task CompleteTurnAsync_ShouldOmitToolsAndDynamicBlock_WhenEmpty()
    {
        var (gateway, handler) = CreateGateway(HttpStatusCode.OK, "{\"choices\":[]}");
        AiChatRequest request = BuildRequest(withTools: false) with { DynamicSessionContext = " " };

        var result = await gateway.CompleteTurnAsync(request);

        result.ReplyText.Should().BeEmpty();
        result.CacheHit.Should().BeFalse();
        using JsonDocument payload = JsonDocument.Parse(handler.RequestBody!);
        payload.RootElement.TryGetProperty("tools", out _).Should().BeFalse();
        payload.RootElement.GetProperty("messages").EnumerateArray()
            .Count(m => m.GetProperty("role").GetString() == "developer").Should().Be(1);
    }

    [Fact]
    public async Task CompleteTurnAsync_ShouldParseToolCalls_WhenModelRequestsTools()
    {
        const string body = "{\"choices\":[{\"message\":{\"content\":null,\"tool_calls\":[" +
            "{\"id\":\"call_1\",\"type\":\"function\",\"function\":{\"name\":\"search_offers\",\"arguments\":\"{\\\"query\\\":\\\"tortas\\\"}\"}}]}}]," +
            "\"usage\":{\"prompt_tokens\":900,\"completion_tokens\":12}}";
        var (gateway, _) = CreateGateway(HttpStatusCode.OK, body);

        var result = await gateway.CompleteTurnAsync(BuildRequest());

        result.ReplyText.Should().BeEmpty();
        result.ToolCalls.Should().ContainSingle();
        result.ToolCalls[0].Id.Should().Be("call_1");
        result.ToolCalls[0].ToolName.Should().Be("search_offers");
        result.ToolCalls[0].ArgumentsJson.Should().Contain("tortas");
        result.CachedTokens.Should().Be(0);
        result.CacheHit.Should().BeFalse();
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.Unauthorized)]
    public async Task CompleteTurnAsync_ShouldThrowHttpRequestException_WhenOpenAiFails(HttpStatusCode status)
    {
        var (gateway, _) = CreateGateway(status, "{\"error\":{\"message\":\"fallo\"}}");

        var act = () => gateway.CompleteTurnAsync(BuildRequest());

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task CompleteTurnAsync_ShouldThrow_WhenResponseIsMalformed()
    {
        var (gateway, _) = CreateGateway(HttpStatusCode.OK, "no-es-json");

        var act = () => gateway.CompleteTurnAsync(BuildRequest());

        await act.Should().ThrowAsync<JsonException>();
    }
}
