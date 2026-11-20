using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Sensus.Application.Common.Settings;
using Sensus.Infrastructure.Services;
using Xunit;

namespace Sensus.IntegrationTests.Services;

public sealed class OpenAiTextModerationServiceTests
{
    private readonly ILogger<OpenAiTextModerationService> _logger = Substitute.For<ILogger<OpenAiTextModerationService>>();

    private sealed class DelegatingMockHandler(Func<HttpRequestMessage, HttpResponseMessage> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(handler(request));
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task ModerateTextAsync_WhenTextIsNullOrWhitespace_ReturnsApproved(string? text)
    {
        // Arrange
        var httpClient = new HttpClient(new DelegatingMockHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)));
        var openAiOptions = Options.Create(new OpenAiSettings { ApiKey = "test-key" });
        var sensiOptions = Options.Create(new SensiSettings());
        var service = new OpenAiTextModerationService(httpClient, openAiOptions, sensiOptions, _logger);

        // Act
        var result = await service.ModerateTextAsync(text!);

        // Assert
        result.IsApproved.Should().BeTrue();
        result.Decision.Should().Be("Approved");
    }

    [Fact]
    public async Task ModerateTextAsync_WhenNoApiKeyConfigured_ReturnsQuarantine_FailClosed()
    {
        // Arrange
        var prevEnv = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        try
        {
            Environment.SetEnvironmentVariable("OPENAI_API_KEY", null);
            var httpClient = new HttpClient(new DelegatingMockHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)));
            var openAiOptions = Options.Create(new OpenAiSettings { ApiKey = "" });
            var sensiOptions = Options.Create(new SensiSettings());
            var service = new OpenAiTextModerationService(httpClient, openAiOptions, sensiOptions, _logger);

            // Act
            var result = await service.ModerateTextAsync("Hola, busco ofertas");

            // Assert
            result.IsApproved.Should().BeFalse();
            result.Decision.Should().Be("Quarantine");
            result.Message.Should().Be(OpenAiTextModerationService.QuarantineMessage);
        }
        finally
        {
            Environment.SetEnvironmentVariable("OPENAI_API_KEY", prevEnv);
        }
    }

    [Fact]
    public async Task ModerateTextAsync_WhenOpenAiApprovesText_ReturnsApproved()
    {
        // Arrange
        var jsonResponse = JsonSerializer.Serialize(new
        {
            id = "modr-text-123",
            model = "omni-moderation-latest",
            results = new[]
            {
                new
                {
                    flagged = false,
                    categories = new Dictionary<string, bool>
                    {
                        ["violence"] = false,
                        ["hate"] = false
                    },
                    category_scores = new Dictionary<string, double>
                    {
                        ["violence"] = 0.01,
                        ["hate"] = 0.02
                    }
                }
            }
        });

        var handler = new DelegatingMockHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(jsonResponse, Encoding.UTF8, "application/json")
        });

        var httpClient = new HttpClient(handler);
        var openAiOptions = Options.Create(new OpenAiSettings { ApiKey = "sk-valid-key" });
        var sensiOptions = Options.Create(new SensiSettings());
        var service = new OpenAiTextModerationService(httpClient, openAiOptions, sensiOptions, _logger);

        // Act
        var result = await service.ModerateTextAsync("¿Tienen paquetes de despensa para 100 pesos?");

        // Assert
        result.IsApproved.Should().BeTrue();
        result.Decision.Should().Be("Approved");
        result.Message.Should().Be("Texto aprobado.");
    }

    [Fact]
    public async Task ModerateTextAsync_WhenOpenAiFlagsText_ReturnsRejectedWithCategories()
    {
        // Arrange
        var jsonResponse = JsonSerializer.Serialize(new
        {
            id = "modr-text-flagged",
            model = "omni-moderation-latest",
            results = new[]
            {
                new
                {
                    flagged = true,
                    categories = new Dictionary<string, bool>
                    {
                        ["violence"] = true,
                        ["hate"] = false
                    },
                    category_scores = new Dictionary<string, double>
                    {
                        ["violence"] = 0.95
                    }
                }
            }
        });

        var handler = new DelegatingMockHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(jsonResponse, Encoding.UTF8, "application/json")
        });

        var httpClient = new HttpClient(handler);
        var openAiOptions = Options.Create(new OpenAiSettings { ApiKey = "sk-valid-key" });
        var sensiOptions = Options.Create(new SensiSettings());
        var service = new OpenAiTextModerationService(httpClient, openAiOptions, sensiOptions, _logger);

        // Act
        var result = await service.ModerateTextAsync("Texto violento de prueba");

        // Assert
        result.IsApproved.Should().BeFalse();
        result.Decision.Should().Be("Rejected");
        result.Message.Should().Be(OpenAiTextModerationService.PolicyViolationMessage);
        result.FlaggedCategories.Should().Contain("violence");
    }

    [Fact]
    public async Task ModerateTextAsync_WhenScoresFallInIntermediateBand_ReturnsQuarantine()
    {
        // Arrange
        var jsonResponse = JsonSerializer.Serialize(new
        {
            id = "modr-text-mid",
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
                        ["sexual"] = 0.50
                    }
                }
            }
        });

        var handler = new DelegatingMockHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(jsonResponse, Encoding.UTF8, "application/json")
        });

        var httpClient = new HttpClient(handler);
        var openAiOptions = Options.Create(new OpenAiSettings
        {
            ApiKey = "sk-valid-key",
            DefaultLowThreshold = 0.35,
            DefaultHighThreshold = 0.75
        });
        var sensiOptions = Options.Create(new SensiSettings());
        var service = new OpenAiTextModerationService(httpClient, openAiOptions, sensiOptions, _logger);

        // Act
        var result = await service.ModerateTextAsync("Texto dudoso");

        // Assert
        result.IsApproved.Should().BeFalse();
        result.Decision.Should().Be("Quarantine");
        result.Message.Should().Be(OpenAiTextModerationService.QuarantineMessage);
        result.FlaggedCategories.Should().Contain("sexual");
    }

    [Fact]
    public async Task ModerateTextAsync_WhenTimeoutOccurs_ReturnsQuarantine_FailClosed()
    {
        // Arrange
        var handler = new DelegatingMockHandler(_ => throw new OperationCanceledException());
        var httpClient = new HttpClient(handler);
        var openAiOptions = Options.Create(new OpenAiSettings { ApiKey = "sk-valid-key" });
        var sensiOptions = Options.Create(new SensiSettings());
        var service = new OpenAiTextModerationService(httpClient, openAiOptions, sensiOptions, _logger);

        // Act
        var result = await service.ModerateTextAsync("Consulta con timeout");

        // Assert
        result.IsApproved.Should().BeFalse();
        result.Decision.Should().Be("Quarantine");
        result.Message.Should().Be(OpenAiTextModerationService.QuarantineMessage);
    }

    [Fact]
    public async Task ModerateTextAsync_WhenHttp500_ReturnsQuarantine_FailClosed()
    {
        // Arrange
        var handler = new DelegatingMockHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = new StringContent("Server error", Encoding.UTF8, "text/plain")
        });

        var httpClient = new HttpClient(handler);
        var openAiOptions = Options.Create(new OpenAiSettings { ApiKey = "sk-valid-key" });
        var sensiOptions = Options.Create(new SensiSettings());
        var service = new OpenAiTextModerationService(httpClient, openAiOptions, sensiOptions, _logger);

        // Act
        var result = await service.ModerateTextAsync("Consulta con error 500");

        // Assert
        result.IsApproved.Should().BeFalse();
        result.Decision.Should().Be("Quarantine");
        result.Message.Should().Be(OpenAiTextModerationService.QuarantineMessage);
    }

    [Fact]
    public async Task ModerateTextAsync_WhenResultsArrayIsEmpty_ReturnsQuarantine_FailClosed()
    {
        // Arrange
        var jsonResponse = JsonSerializer.Serialize(new
        {
            id = "modr-empty",
            model = "omni-moderation-latest",
            results = Array.Empty<object>()
        });

        var handler = new DelegatingMockHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(jsonResponse, Encoding.UTF8, "application/json")
        });

        var httpClient = new HttpClient(handler);
        var openAiOptions = Options.Create(new OpenAiSettings { ApiKey = "sk-valid-key" });
        var sensiOptions = Options.Create(new SensiSettings());
        var service = new OpenAiTextModerationService(httpClient, openAiOptions, sensiOptions, _logger);

        // Act
        var result = await service.ModerateTextAsync("Consulta con resultado vacio");

        // Assert
        result.IsApproved.Should().BeFalse();
        result.Decision.Should().Be("Quarantine");
        result.Message.Should().Be(OpenAiTextModerationService.QuarantineMessage);
    }

    [Fact]
    public async Task ModerateTextAsync_WhenMalformedJson_ReturnsQuarantine_FailClosed()
    {
        // Arrange
        var handler = new DelegatingMockHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{ not valid json }", Encoding.UTF8, "application/json")
        });

        var httpClient = new HttpClient(handler);
        var openAiOptions = Options.Create(new OpenAiSettings { ApiKey = "sk-valid-key" });
        var sensiOptions = Options.Create(new SensiSettings());
        var service = new OpenAiTextModerationService(httpClient, openAiOptions, sensiOptions, _logger);

        // Act
        var result = await service.ModerateTextAsync("Consulta con json roto");

        // Assert
        result.IsApproved.Should().BeFalse();
        result.Decision.Should().Be("Quarantine");
        result.Message.Should().Be(OpenAiTextModerationService.QuarantineMessage);
    }

    [Fact]
    public async Task ModerateTextAsync_WhenSensiTextModerationThresholdsAreConfigured_UsesSensiThresholds()
    {
        // Arrange: Sensi tiene un umbral estricto para hate (Low: 0.10, High: 0.30)
        var jsonResponse = JsonSerializer.Serialize(new
        {
            id = "modr-text-456",
            model = "omni-moderation-latest",
            results = new[]
            {
                new
                {
                    flagged = false,
                    categories = new Dictionary<string, bool> { ["hate"] = false },
                    category_scores = new Dictionary<string, double> { ["hate"] = 0.20 } // Entre 0.10 y 0.30 -> Quarantine
                }
            }
        });

        var handler = new DelegatingMockHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(jsonResponse, Encoding.UTF8, "application/json")
        });

        var httpClient = new HttpClient(handler);
        var openAiOptions = Options.Create(new OpenAiSettings
        {
            ApiKey = "test-key",
            CategoryThresholds = new() { ["hate"] = new CategoryThreshold { Low = 0.50, High = 0.90 } }, // Ignorado para texto
            Sensi = new SensiSettings
            {
                TextModerationThresholds = new()
                {
                    ["hate"] = new TextCategoryThreshold { Low = 0.10, High = 0.30 }
                }
            }
        });
        var sensiOptions = Options.Create(openAiOptions.Value.Sensi);
        var service = new OpenAiTextModerationService(httpClient, openAiOptions, sensiOptions, _logger);

        // Act
        var result = await service.ModerateTextAsync("Texto sospechoso");

        // Assert
        result.IsApproved.Should().BeFalse();
        result.Decision.Should().Be("Quarantine");
    }

    [Fact]
    public async Task ModerateContentTextAsync_WithGptModel_ShouldApproveCleanName()
    {
        // Arrange
        var chatJson = JsonSerializer.Serialize(new
        {
            choices = new[]
            {
                new
                {
                    message = new
                    {
                        content = """{"decision":"Approved","reason":"Nombre válido.","flagged_categories":[]}"""
                    }
                }
            }
        });

        Uri? capturedUri = null;
        var handler = new DelegatingMockHandler(req =>
        {
            capturedUri = req.RequestUri;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(chatJson, Encoding.UTF8, "application/json")
            };
        });

        var httpClient = new HttpClient(handler);
        var openAiOptions = Options.Create(new OpenAiSettings
        {
            ApiKey = "sk-valid-key",
            Model = "gpt-4o-mini"
        });
        var sensiOptions = Options.Create(new SensiSettings());
        var service = new OpenAiTextModerationService(httpClient, openAiOptions, sensiOptions, _logger);

        // Act
        var result = await service.ModerateContentTextAsync("Panadería La Esperanza", "Registro de comercio");

        // Assert
        capturedUri?.AbsolutePath.Should().EndWith("/chat/completions");
        result.IsApproved.Should().BeTrue();
        result.Decision.Should().Be("Approved");
    }

    [Fact]
    public async Task ModerateContentTextAsync_WithGptModel_ShouldRejectProfanityWithReason()
    {
        // Arrange
        var chatJson = JsonSerializer.Serialize(new
        {
            choices = new[]
            {
                new
                {
                    message = new
                    {
                        content = """{"decision":"Rejected","reason":"Contiene palabras antisonantes u ofensivas.","flagged_categories":["profanity"]}"""
                    }
                }
            }
        });

        var handler = new DelegatingMockHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(chatJson, Encoding.UTF8, "application/json")
        });

        var httpClient = new HttpClient(handler);
        var openAiOptions = Options.Create(new OpenAiSettings
        {
            ApiKey = "sk-valid-key",
            Model = "gpt-4o-mini"
        });
        var sensiOptions = Options.Create(new SensiSettings());
        var service = new OpenAiTextModerationService(httpClient, openAiOptions, sensiOptions, _logger);

        // Act
        var result = await service.ModerateContentTextAsync("Palabra ofensiva de prueba", "Nombre de usuario");

        // Assert
        result.IsApproved.Should().BeFalse();
        result.Decision.Should().Be("Rejected");
        result.Message.Should().Be("Contiene palabras antisonantes u ofensivas.");
        result.FlaggedCategories.Should().Contain("profanity");
    }

    [Fact]
    public async Task ModerateContentTextAsync_WithGptModel_WhenMalformedJson_ReturnsQuarantine_FailClosed()
    {
        // Arrange
        var chatJson = JsonSerializer.Serialize(new
        {
            choices = new[]
            {
                new
                {
                    message = new
                    {
                        content = "{invalid-json"
                    }
                }
            }
        });

        var handler = new DelegatingMockHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(chatJson, Encoding.UTF8, "application/json")
        });

        var httpClient = new HttpClient(handler);
        var openAiOptions = Options.Create(new OpenAiSettings
        {
            ApiKey = "sk-valid-key",
            Model = "gpt-4o-mini"
        });
        var sensiOptions = Options.Create(new SensiSettings());
        var service = new OpenAiTextModerationService(httpClient, openAiOptions, sensiOptions, _logger);

        // Act
        var result = await service.ModerateContentTextAsync("Oferta de prueba", "Publicación de producto");

        // Assert
        result.IsApproved.Should().BeFalse();
        result.Decision.Should().Be("Quarantine");
        result.Message.Should().Be(OpenAiTextModerationService.QuarantineMessage);
    }
}
