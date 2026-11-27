using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Sensus.Infrastructure.Services;
using Xunit;

namespace Sensus.IntegrationTests.Services;

public sealed class OpenAiImageModerationServiceTests
{
    private readonly ILogger<OpenAiImageModerationService> _logger = Substitute.For<ILogger<OpenAiImageModerationService>>();
    private static readonly string[] WeaponsCategory = ["weapons"];

    private sealed class DelegatingMockHandler(Func<HttpRequestMessage, HttpResponseMessage> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(handler(request));
        }
    }

    private sealed class AsyncMockHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            handler(request, cancellationToken);
    }

    [Theory]
    [InlineData("""{"decision":"Approved","flagged_categories":[],"category_scores":{"weapons":0.0}}""", "Approved")]
    [InlineData("""{"decision":"Rejected","flagged_categories":["weapons"],"category_scores":{"weapons":0.95}}""", "Rejected")]
    [InlineData("""{"decision":"Approved","flagged_categories":[]}""", "Quarantine")]
    [InlineData("""{"decision":"Unexpected","flagged_categories":[],"category_scores":{"weapons":0}}""", "Quarantine")]
    [InlineData("""{"decision":"Approved","flagged_categories":[],"category_scores":{"weapons":2}}""", "Quarantine")]
    [InlineData("""{"decision":"Approved","flagged_categories":["weapons"],"category_scores":{"weapons":0.95}}""", "Quarantine")]
    [InlineData("invalid-json", "Quarantine")]
    [InlineData("", "Quarantine")]
    public async Task ModerateTextAsync_SendsOnlyTextAndValidatesVerdict(string content, string expectedDecision)
    {
        string? requestBody = null;
        var handler = new AsyncMockHandler(async (request, cancellationToken) =>
        {
            request.RequestUri!.AbsolutePath.Should().Be("/v1/chat/completions");
            requestBody = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(new
                {
                    choices = new[] { new { message = new { content } } }
                }), Encoding.UTF8, "application/json")
            };
        });
        using var httpClient = new HttpClient(handler);
        var service = new OpenAiImageModerationService(httpClient,
            Options.Create(new OpenAiSettings { ApiKey = "test-placeholder", Model = "gpt-4o-mini" }), _logger);

        var result = await service.ModerateTextAsync("Hoy hay pan recién horneado.");

        result.Decision.Should().Be(expectedDecision);
        result.IsApproved.Should().Be(expectedDecision == "Approved");
        using var body = JsonDocument.Parse(requestBody!);
        body.RootElement.GetProperty("model").GetString().Should().Be("gpt-4o-mini");
        var userContent = body.RootElement.GetProperty("messages")[1].GetProperty("content");
        userContent.GetArrayLength().Should().Be(1);
        userContent[0].GetProperty("type").GetString().Should().Be("text");
        userContent[0].GetProperty("text").GetString().Should().Contain("Hoy hay pan recién horneado.");
    }

    [Fact]
    public async Task ModerateTextAsync_WhenProviderTimesOut_ReturnsQuarantine()
    {
        using var httpClient = new HttpClient(new DelegatingMockHandler(_ => throw new TaskCanceledException()));
        var service = new OpenAiImageModerationService(httpClient,
            Options.Create(new OpenAiSettings { ApiKey = "test-placeholder", Model = "gpt-4o-mini" }), _logger);

        var result = await service.ModerateTextAsync("Publicación de prueba");

        result.IsApproved.Should().BeFalse();
        result.Decision.Should().Be("Quarantine");
    }

    [Fact]
    public async Task ModerateImageBytesAsync_WhenBytesAreEmpty_ReturnsRejected()
    {
        // Arrange
        var httpClient = new HttpClient(new DelegatingMockHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)));
        var options = Options.Create(new OpenAiSettings { ApiKey = "test-key" });
        var service = new OpenAiImageModerationService(httpClient, options, _logger);

        // Act
        var result = await service.ModerateImageBytesAsync([]);

        // Assert
        result.IsApproved.Should().BeFalse();
        result.Decision.Should().Be("Rejected");
        result.Message.Should().Be("La imagen no puede estar vacía.");
    }

    [Fact]
    public async Task ModerateImageBytesAsync_WhenNoApiKeyConfigured_ReturnsQuarantine_FailClosed()
    {
        // Arrange
        var prevEnv = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        try
        {
            Environment.SetEnvironmentVariable("OPENAI_API_KEY", null);
            var httpClient = new HttpClient(new DelegatingMockHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)));
            var options = Options.Create(new OpenAiSettings { ApiKey = "" });
            var service = new OpenAiImageModerationService(httpClient, options, _logger);

            // Act
            var result = await service.ModerateImageBytesAsync([1, 2, 3]);

            // Assert
            result.IsApproved.Should().BeFalse();
            result.Decision.Should().Be("Quarantine");
            result.Message.Should().Be(OpenAiImageModerationService.QuarantineMessage);
        }
        finally
        {
            Environment.SetEnvironmentVariable("OPENAI_API_KEY", prevEnv);
        }
    }

    [Fact]
    public async Task ModerateImageBytesAsync_WhenOpenAiApprovesImage_ReturnsApproved()
    {
        // Arrange
        var jsonResponse = JsonSerializer.Serialize(new
        {
            id = "modr-123",
            model = "omni-moderation-latest",
            results = new[]
            {
                new
                {
                    flagged = false,
                    categories = new Dictionary<string, bool>
                    {
                        ["hate"] = false,
                        ["violence"] = false
                    },
                    category_scores = new Dictionary<string, double>
                    {
                        ["hate"] = 0.01,
                        ["violence"] = 0.02
                    }
                }
            }
        });

        var handler = new DelegatingMockHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(jsonResponse, Encoding.UTF8, "application/json")
        });

        var httpClient = new HttpClient(handler);
        var options = Options.Create(new OpenAiSettings { ApiKey = "sk-valid-key" });
        var service = new OpenAiImageModerationService(httpClient, options, _logger);

        // Act
        var result = await service.ModerateImageBytesAsync([10, 20, 30], "image/jpeg");

        // Assert
        result.IsApproved.Should().BeTrue();
        result.Decision.Should().Be("Approved");
        result.Message.Should().Be("Imagen aprobada.");
    }

    [Theory]
    [InlineData("""{"categories":{"hate":false},"category_scores":{"hate":0.1}}""")]
    [InlineData("""{"flagged":false,"categories":{"hate":false}}""")]
    [InlineData("""{"flagged":false,"categories":{"hate":false},"category_scores":{"hate":1.1}}""")]
    [InlineData("""{"flagged":false,"categories":{"hate":true},"category_scores":{"hate":0.1}}""")]
    [InlineData("""{"flagged":false,"categories":{"hate":false},"category_scores":{"violence":0.1}}""")]
    public async Task ModerateImageBytesAsync_WhenOmniVerdictIsIncomplete_ReturnsQuarantine(string result)
    {
        using var httpClient = new HttpClient(new DelegatingMockHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent($$"""{"results":[{{result}}]}""", Encoding.UTF8, "application/json")
        }));
        var service = new OpenAiImageModerationService(httpClient,
            Options.Create(new OpenAiSettings { ApiKey = "test-placeholder", Model = "omni-moderation-latest" }), _logger);

        var verdict = await service.ModerateImageBytesAsync([1, 2, 3]);

        verdict.IsApproved.Should().BeFalse();
        verdict.Decision.Should().Be("Quarantine");
    }

    [Fact]
    public async Task ModerateImageBytesAsync_WhenOpenAiFlagsImage_ReturnsRejectedWithCategories()
    {
        // Arrange
        var jsonResponse = JsonSerializer.Serialize(new
        {
            id = "modr-456",
            model = "omni-moderation-latest",
            results = new[]
            {
                new
                {
                    flagged = true,
                    categories = new Dictionary<string, bool>
                    {
                        ["hate"] = true,
                        ["violence"] = false
                    },
                    category_scores = new Dictionary<string, double>
                    {
                        ["hate"] = 0.92,
                        ["violence"] = 0.01
                    }
                }
            }
        });

        var handler = new DelegatingMockHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(jsonResponse, Encoding.UTF8, "application/json")
        });

        var httpClient = new HttpClient(handler);
        var options = Options.Create(new OpenAiSettings { ApiKey = "sk-valid-key" });
        var service = new OpenAiImageModerationService(httpClient, options, _logger);

        // Act
        var result = await service.ModerateImageBytesAsync([10, 20, 30], "image/png");

        // Assert
        result.IsApproved.Should().BeFalse();
        result.Decision.Should().Be("Rejected");
        result.Message.Should().Be(OpenAiImageModerationService.PolicyViolationMessage);
        result.FlaggedCategories.Should().Contain("hate");
    }

    [Fact]
    public async Task ModerateImageBytesAsync_WhenCategoryScoresExceedHighThreshold_ReturnsRejected()
    {
        // Arrange
        var jsonResponse = JsonSerializer.Serialize(new
        {
            id = "modr-score-high",
            model = "omni-moderation-latest",
            results = new[]
            {
                new
                {
                    flagged = false,
                    categories = new Dictionary<string, bool>
                    {
                        ["violence"] = false
                    },
                    category_scores = new Dictionary<string, double>
                    {
                        ["violence"] = 0.88
                    }
                }
            }
        });

        var handler = new DelegatingMockHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(jsonResponse, Encoding.UTF8, "application/json")
        });

        var httpClient = new HttpClient(handler);
        var options = Options.Create(new OpenAiSettings
        {
            ApiKey = "sk-valid-key",
            DefaultHighThreshold = 0.75
        });
        var service = new OpenAiImageModerationService(httpClient, options, _logger);

        // Act
        var result = await service.ModerateImageBytesAsync([1, 2, 3]);

        // Assert
        result.IsApproved.Should().BeFalse();
        result.Decision.Should().Be("Rejected");
        result.FlaggedCategories.Should().Contain("violence");
        result.Message.Should().Be(OpenAiImageModerationService.PolicyViolationMessage);
    }

    [Fact]
    public async Task ModerateImageBytesAsync_WhenCategoryScoresFallInIntermediateBand_ReturnsQuarantine()
    {
        // Arrange
        var jsonResponse = JsonSerializer.Serialize(new
        {
            id = "modr-score-mid",
            model = "omni-moderation-latest",
            results = new[]
            {
                new
                {
                    flagged = false,
                    categories = new Dictionary<string, bool>
                    {
                        ["sexual"] = false
                    },
                    category_scores = new Dictionary<string, double>
                    {
                        ["sexual"] = 0.52
                    }
                }
            }
        });

        var handler = new DelegatingMockHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(jsonResponse, Encoding.UTF8, "application/json")
        });

        var httpClient = new HttpClient(handler);
        var options = Options.Create(new OpenAiSettings
        {
            ApiKey = "sk-valid-key",
            DefaultLowThreshold = 0.35,
            DefaultHighThreshold = 0.75
        });
        var service = new OpenAiImageModerationService(httpClient, options, _logger);

        // Act
        var result = await service.ModerateImageBytesAsync([1, 2, 3]);

        // Assert
        result.IsApproved.Should().BeFalse();
        result.Decision.Should().Be("Quarantine");
        result.FlaggedCategories.Should().Contain("sexual");
        result.Message.Should().Be(OpenAiImageModerationService.QuarantineMessage);
    }

    [Fact]
    public async Task ModerateImageBytesAsync_WhenTextContextIsProvided_SendsBothTextAndImageInPayload()
    {
        // Arrange
        string capturedBody = string.Empty;
        var jsonResponse = JsonSerializer.Serialize(new
        {
            id = "modr-multi",
            model = "omni-moderation-latest",
            results = new[]
            {
                new
                {
                    flagged = false,
                    categories = new Dictionary<string, bool> { ["hate"] = false },
                    category_scores = new Dictionary<string, double> { ["hate"] = 0.01 }
                }
            }
        });

        var handler = new DelegatingMockHandler(req =>
        {
            if (req.Content != null)
            {
                capturedBody = req.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            }
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(jsonResponse, Encoding.UTF8, "application/json")
            };
        });

        var httpClient = new HttpClient(handler);
        var options = Options.Create(new OpenAiSettings { ApiKey = "sk-valid-key" });
        var service = new OpenAiImageModerationService(httpClient, options, _logger);

        // Act
        var result = await service.ModerateImageBytesAsync([1, 2, 3], "image/jpeg", "Oferta de tacos al pastor");

        // Assert
        result.IsApproved.Should().BeTrue();
        capturedBody.Should().NotBeEmpty();
        capturedBody.Should().Contain("Oferta de tacos al pastor");
        capturedBody.Should().Contain("image_url");
        capturedBody.Should().Contain("text");
    }

    [Fact]
    public async Task ModerateImageBytesAsync_WhenResultsArrayIsEmpty_ReturnsQuarantine_FailClosed()
    {
        // Arrange
        var jsonResponse = JsonSerializer.Serialize(new
        {
            id = "modr-789",
            model = "omni-moderation-latest",
            results = Array.Empty<object>()
        });

        var handler = new DelegatingMockHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(jsonResponse, Encoding.UTF8, "application/json")
        });

        var httpClient = new HttpClient(handler);
        var options = Options.Create(new OpenAiSettings { ApiKey = "sk-valid-key" });
        var service = new OpenAiImageModerationService(httpClient, options, _logger);

        // Act
        var result = await service.ModerateImageBytesAsync([1, 2, 3]);

        // Assert
        result.IsApproved.Should().BeFalse();
        result.Decision.Should().Be("Quarantine");
        result.Message.Should().Be(OpenAiImageModerationService.QuarantineMessage);
    }

    [Fact]
    public async Task ModerateImageBytesAsync_WhenHttpReturns500_ReturnsQuarantine_FailClosed()
    {
        // Arrange
        var handler = new DelegatingMockHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = new StringContent("Server error", Encoding.UTF8, "text/plain")
        });

        var httpClient = new HttpClient(handler);
        var options = Options.Create(new OpenAiSettings { ApiKey = "sk-valid-key" });
        var service = new OpenAiImageModerationService(httpClient, options, _logger);

        // Act
        var result = await service.ModerateImageBytesAsync([1, 2, 3]);

        // Assert
        result.IsApproved.Should().BeFalse();
        result.Decision.Should().Be("Quarantine");
        result.Message.Should().Be(OpenAiImageModerationService.QuarantineMessage);
    }

    [Fact]
    public async Task ModerateImageBytesAsync_WhenTimeoutOccurs_ReturnsQuarantine_FailClosed()
    {
        // Arrange
        var handler = new DelegatingMockHandler(_ => throw new OperationCanceledException());
        var httpClient = new HttpClient(handler);
        var options = Options.Create(new OpenAiSettings { ApiKey = "sk-valid-key" });
        var service = new OpenAiImageModerationService(httpClient, options, _logger);

        // Act
        var result = await service.ModerateImageBytesAsync([1, 2, 3]);

        // Assert
        result.IsApproved.Should().BeFalse();
        result.Decision.Should().Be("Quarantine");
        result.Message.Should().Be(OpenAiImageModerationService.QuarantineMessage);
    }

    [Fact]
    public async Task ModerateImageBytesAsync_WithGpt4oMini_WhenWeaponDetected_ReturnsRejected()
    {
        // Arrange
        var chatContent = JsonSerializer.Serialize(new
        {
            decision = "Rejected",
            reason = "Se detectó presencia de un arma de fuego.",
            flagged_categories = WeaponsCategory,
            category_scores = new Dictionary<string, double> { ["weapons"] = 0.95 }
        });

        var jsonResponse = JsonSerializer.Serialize(new
        {
            choices = new[]
            {
                new
                {
                    message = new
                    {
                        content = chatContent
                    }
                }
            }
        });

        var handler = new DelegatingMockHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(jsonResponse, Encoding.UTF8, "application/json")
        });

        var httpClient = new HttpClient(handler);
        var options = Options.Create(new OpenAiSettings { ApiKey = "sk-valid-key", Model = "gpt-4o-mini" });
        var service = new OpenAiImageModerationService(httpClient, options, _logger);

        // Act
        var result = await service.ModerateImageBytesAsync([1, 2, 3], "image/jpeg", "Oferta de pistola 9mm");

        // Assert
        result.IsApproved.Should().BeFalse();
        result.Decision.Should().Be("Rejected");
        result.Message.Should().Be("Se detectó presencia de un arma de fuego.");
        result.FlaggedCategories.Should().Contain("weapons");
        result.CategoryScores.Should().ContainKey("weapons");
    }

    [Fact]
    public async Task ModerateImageBytesAsync_WithGpt4oMini_WhenCleanProduct_ReturnsApproved()
    {
        // Arrange
        var chatContent = JsonSerializer.Serialize(new
        {
            decision = "Approved",
            reason = "Producto comercial seguro.",
            flagged_categories = Array.Empty<string>(),
            category_scores = new Dictionary<string, double> { ["weapons"] = 0.0, ["hate"] = 0.0 }
        });

        var jsonResponse = JsonSerializer.Serialize(new
        {
            choices = new[]
            {
                new
                {
                    message = new
                    {
                        content = chatContent
                    }
                }
            }
        });

        var handler = new DelegatingMockHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(jsonResponse, Encoding.UTF8, "application/json")
        });

        var httpClient = new HttpClient(handler);
        var options = Options.Create(new OpenAiSettings { ApiKey = "sk-valid-key", Model = "gpt-4o-mini" });
        var service = new OpenAiImageModerationService(httpClient, options, _logger);

        // Act
        var result = await service.ModerateImageBytesAsync([1, 2, 3], "image/jpeg", "Hamburguesa clásica");

        // Assert
        result.IsApproved.Should().BeTrue();
        result.Decision.Should().Be("Approved");
        result.Message.Should().Be("Imagen aprobada.");
    }

    [Theory]
    [InlineData("""{"decision":"Approved","reason":"Imagen de producto válida.","flagged_categories":[]}""")]
    [InlineData("""{"decision":"Approved","reason":"Imagen segura.","flagged_categories":[],"category_scores":{}}""")]
    public async Task ModerateImageBytesAsync_WithGpt4oMini_WhenApprovedWithoutCategoryScores_ReturnsApproved(string chatContent)
    {
        var jsonResponse = JsonSerializer.Serialize(new
        {
            choices = new[] { new { message = new { content = chatContent } } }
        });
        var handler = new DelegatingMockHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(jsonResponse, Encoding.UTF8, "application/json")
        });
        using var httpClient = new HttpClient(handler);
        var service = new OpenAiImageModerationService(
            httpClient,
            Options.Create(new OpenAiSettings { ApiKey = "sk-valid-key", Model = "gpt-4o-mini" }),
            _logger);

        var result = await service.ModerateImageBytesAsync([1, 2, 3], "image/jpeg", "Panadería artesanal");

        result.IsApproved.Should().BeTrue();
        result.Decision.Should().Be("Approved");
        result.Message.Should().Be("Imagen aprobada.");
    }

    [Fact]
    public async Task ModerateImageBytesAsync_WithGpt4oMini_WhenSuspicious_ReturnsQuarantine()
    {
        // Arrange
        var chatContent = JsonSerializer.Serialize(new
        {
            decision = "Quarantine",
            reason = "Objeto sospechoso similar a réplica.",
            flagged_categories = WeaponsCategory,
            category_scores = new Dictionary<string, double> { ["weapons"] = 0.45 }
        });

        var jsonResponse = JsonSerializer.Serialize(new
        {
            choices = new[]
            {
                new
                {
                    message = new
                    {
                        content = chatContent
                    }
                }
            }
        });

        var handler = new DelegatingMockHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(jsonResponse, Encoding.UTF8, "application/json")
        });

        var httpClient = new HttpClient(handler);
        var options = Options.Create(new OpenAiSettings { ApiKey = "sk-valid-key", Model = "gpt-4o-mini" });
        var service = new OpenAiImageModerationService(httpClient, options, _logger);

        // Act
        var result = await service.ModerateImageBytesAsync([1, 2, 3], "image/jpeg", "Juguete de colección");

        // Assert
        result.IsApproved.Should().BeFalse();
        result.Decision.Should().Be("Quarantine");
        result.Message.Should().Be("Objeto sospechoso similar a réplica.");
        result.FlaggedCategories.Should().Contain("weapons");
    }

    [Fact]
    public async Task ModerateImageBytesAsync_WhenTransient503Then200_RetriesOnceAndReturnsApproved()
    {
        var chatContent = JsonSerializer.Serialize(new
        {
            decision = "Approved",
            flagged_categories = Array.Empty<string>(),
            category_scores = new Dictionary<string, double> { ["weapons"] = 0.0 }
        });
        var jsonResponse = JsonSerializer.Serialize(new
        {
            choices = new[] { new { message = new { content = chatContent } } }
        });

        var calls = 0;
        var handler = new DelegatingMockHandler(_ =>
        {
            calls++;
            return calls == 1
                ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable) { Content = new StringContent("busy", Encoding.UTF8, "text/plain") }
                : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(jsonResponse, Encoding.UTF8, "application/json") };
        });

        using var httpClient = new HttpClient(handler);
        var options = Options.Create(new OpenAiSettings { ApiKey = "sk-valid-key", Model = "gpt-4o-mini", TimeoutSeconds = 8 });
        var service = new OpenAiImageModerationService(httpClient, options, _logger);

        var result = await service.ModerateImageBytesAsync([1, 2, 3], "image/jpeg", "Pan dulce");

        calls.Should().Be(2);
        result.IsApproved.Should().BeTrue();
        result.Decision.Should().Be("Approved");
    }

    [Fact]
    public async Task ModerateImageBytesAsync_WhenTransient429WithRetryAfterThen200_RetriesOnceAndReturnsApproved()
    {
        var chatContent = JsonSerializer.Serialize(new
        {
            decision = "Approved",
            flagged_categories = Array.Empty<string>(),
            category_scores = new Dictionary<string, double> { ["weapons"] = 0.0 }
        });
        var jsonResponse = JsonSerializer.Serialize(new
        {
            choices = new[] { new { message = new { content = chatContent } } }
        });

        var calls = 0;
        var handler = new DelegatingMockHandler(_ =>
        {
            calls++;
            if (calls == 1)
            {
                var tooMany = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
                tooMany.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromMilliseconds(50));
                return tooMany;
            }

            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(jsonResponse, Encoding.UTF8, "application/json") };
        });

        using var httpClient = new HttpClient(handler);
        var options = Options.Create(new OpenAiSettings { ApiKey = "sk-valid-key", Model = "gpt-4o-mini", TimeoutSeconds = 8 });
        var service = new OpenAiImageModerationService(httpClient, options, _logger);

        var result = await service.ModerateImageBytesAsync([1, 2, 3], "image/jpeg", "Pan dulce");

        calls.Should().Be(2);
        result.IsApproved.Should().BeTrue();
        result.Decision.Should().Be("Approved");
    }

    [Fact]
    public async Task ModerateImageBytesAsync_WhenTransient503Twice_RetriesOnceAndReturnsQuarantine()
    {
        var calls = 0;
        var handler = new DelegatingMockHandler(_ =>
        {
            calls++;
            return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
        });

        using var httpClient = new HttpClient(handler);
        var options = Options.Create(new OpenAiSettings { ApiKey = "sk-valid-key", Model = "gpt-4o-mini", TimeoutSeconds = 8 });
        var service = new OpenAiImageModerationService(httpClient, options, _logger);

        var result = await service.ModerateImageBytesAsync([1, 2, 3], "image/jpeg", "Pan dulce");

        calls.Should().Be(2);
        result.IsApproved.Should().BeFalse();
        result.Decision.Should().Be("Quarantine");
    }

    [Fact]
    public async Task ModerateImageBytesAsync_WhenMalformedJson_ReturnsQuarantineWithoutRetry()
    {
        var calls = 0;
        var handler = new DelegatingMockHandler(_ =>
        {
            calls++;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{invalid-json", Encoding.UTF8, "application/json")
            };
        });

        using var httpClient = new HttpClient(handler);
        var options = Options.Create(new OpenAiSettings { ApiKey = "sk-valid-key", Model = "gpt-4o-mini", TimeoutSeconds = 8 });
        var service = new OpenAiImageModerationService(httpClient, options, _logger);

        var result = await service.ModerateImageBytesAsync([1, 2, 3], "image/jpeg", "Pan dulce");

        calls.Should().Be(1);
        result.IsApproved.Should().BeFalse();
        result.Decision.Should().Be("Quarantine");
    }
}
