using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Sensus.Api.Extensions;
using Sensus.Api.Middlewares;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Users.Commands.RequestPasswordReset;
using Sensus.Domain.Common;
using Sensus.Domain.Entities;
using Sensus.IntegrationTests.Infrastructure;
using Xunit;

namespace Sensus.IntegrationTests.Middlewares;

/// <summary>
/// Pruebas de integración para garantizar formato uniforme de RFC 7807 (ProblemDetails)
/// y correlationId en todos los códigos de error HTTP: 400, 401, 403, 404, 409, 429 y 500 (Spec 009 / FR-009-01..03).
/// </summary>
public sealed class ProblemDetailsUniformityTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public ProblemDetailsUniformityTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Error400_Validation_ReturnsUniformProblemDetails()
    {
        string correlationId = "corr-400-" + Guid.NewGuid().ToString("N");
        var tokenGen = _factory.Services.GetRequiredService<IJwtTokenGenerator>();
        var user = User.Create("Registered User", "reg@sensus.com", "hash", true, DateTime.UtcNow).Value;
        _factory.UserRepository.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);
        string registeredToken = tokenGen.GenerateToken(user).Token;

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/feed/offers");
        request.Headers.Add("X-Correlation-Id", correlationId);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", registeredToken);

        var response = await _client.SendAsync(request);

        await AssertProblemDetailsShape(response, HttpStatusCode.BadRequest, correlationId);
    }

    [Fact]
    public async Task Error401_Unauthorized_ReturnsUniformProblemDetails()
    {
        string correlationId = "corr-401-" + Guid.NewGuid().ToString("N");
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/merchants/me");
        request.Headers.Add("X-Correlation-Id", correlationId);

        var response = await _client.SendAsync(request);

        await AssertProblemDetailsShape(response, HttpStatusCode.Unauthorized, correlationId);
    }

    [Fact]
    public async Task Error403_Forbidden_ReturnsUniformProblemDetails()
    {
        string correlationId = "corr-403-" + Guid.NewGuid().ToString("N");
        var tokenGen = _factory.Services.GetRequiredService<IJwtTokenGenerator>();
        string guestToken = tokenGen.GenerateGuestToken(Guid.NewGuid()).Token;

        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/merchants/reviews/{Guid.NewGuid()}/like");
        request.Headers.Add("X-Correlation-Id", correlationId);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", guestToken);

        var response = await _client.SendAsync(request);

        await AssertProblemDetailsShape(response, HttpStatusCode.Forbidden, correlationId);
    }

    [Fact]
    public async Task Error404_NotFound_ReturnsUniformProblemDetails()
    {
        string correlationId = "corr-404-" + Guid.NewGuid().ToString("N");
        var nonExistentMerchantId = Guid.NewGuid();
        _factory.ComercioRepository.GetByIdAsync(nonExistentMerchantId, Arg.Any<CancellationToken>())
            .Returns((Comercio?)null);

        var tokenGen = _factory.Services.GetRequiredService<IJwtTokenGenerator>();
        string guestToken = tokenGen.GenerateGuestToken(Guid.NewGuid()).Token;

        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/merchants/{nonExistentMerchantId}/reviews");
        request.Headers.Add("X-Correlation-Id", correlationId);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", guestToken);

        var response = await _client.SendAsync(request);

        await AssertProblemDetailsShape(response, HttpStatusCode.NotFound, correlationId);
    }

    [Fact]
    public async Task Error409_Conflict_ReturnsUniformProblemDetails()
    {
        string correlationId = "corr-409-" + Guid.NewGuid().ToString("N");
        string existingEmail = $"registered.{Guid.NewGuid():N}@sensus.com";

        _factory.UserRepository.ExistsByEmailAsync(existingEmail, Arg.Any<CancellationToken>())
            .Returns(true);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/register");
        request.Headers.Add("X-Correlation-Id", correlationId);
        request.Content = JsonContent.Create(new
        {
            nombreCompleto = "Otro Usuario",
            correo = existingEmail,
            password = "Password123!",
            aceptoTerminos = true
        });

        var response = await _client.SendAsync(request);

        await AssertProblemDetailsShape(response, HttpStatusCode.Conflict, correlationId);
    }

    [Fact]
    public async Task Error429_RateLimited_ReturnsUniformProblemDetails()
    {
        string correlationId = "corr-429-" + Guid.NewGuid().ToString("N");
        string testIp = "198.51.100." + Random.Shared.Next(1, 250);
        string userEmail = $"rl.{Guid.NewGuid():N}@sensus.com";

        var user = User.Create(
            "Rate Limit Test User",
            userEmail,
            "hashed-password",
            true,
            DateTime.UtcNow).Value;

        _factory.UserRepository.GetByEmailAsync(userEmail, Arg.Any<CancellationToken>())
            .Returns(user);

        var command = new RequestPasswordResetCommand(userEmail);

        HttpResponseMessage? response = null;
        for (var i = 0; i < 6; i++)
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/recover")
            {
                Content = JsonContent.Create(command)
            };
            req.Headers.Add("X-Forwarded-For", testIp);
            req.Headers.Add("X-Correlation-Id", correlationId);

            response = await _client.SendAsync(req);
        }

        response.Should().NotBeNull();
        await AssertProblemDetailsShape(response!, (HttpStatusCode)429, correlationId);
    }

    [Fact]
    public async Task Error500_UnhandledException_ReturnsUniformProblemDetailsWithoutStackTrace()
    {
        string correlationId = "corr-500-" + Guid.NewGuid().ToString("N");
        var errorMerchantId = Guid.NewGuid();
        _factory.ComercioRepository.GetByIdAsync(errorMerchantId, Arg.Any<CancellationToken>())
            .Throws(new InvalidOperationException("Fallo interno forzado para prueba"));

        var tokenGen = _factory.Services.GetRequiredService<IJwtTokenGenerator>();
        string guestToken = tokenGen.GenerateGuestToken(Guid.NewGuid()).Token;

        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/merchants/{errorMerchantId}/reviews");
        request.Headers.Add("X-Correlation-Id", correlationId);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", guestToken);

        var response = await _client.SendAsync(request);

        await AssertProblemDetailsShape(response, HttpStatusCode.InternalServerError, correlationId);
    }

    [Fact]
    public async Task ResultExtensions_AllErrorTypesAndOverloads_ProduceExpectedProblemDetails()
    {
        Error[] errors =
        [
            new(string.Empty, "Bad request msg", ErrorType.Validation),
            new(string.Empty, "Unauthorized msg", ErrorType.Unauthorized),
            new(string.Empty, "Forbidden msg", ErrorType.Forbidden),
            new(string.Empty, "Not found msg", ErrorType.NotFound),
            new(string.Empty, "Conflict msg", ErrorType.Conflict),
            Error.PayloadTooLarge(string.Empty, "Payload too large msg"),
            new(string.Empty, "Unprocessable msg", ErrorType.Unprocessable),
            Error.Unavailable(string.Empty, "Unavailable msg"),
            Error.Failure(string.Empty, "Generic failure msg"),
            new(string.Empty, "Unknown enum msg", (ErrorType)999),
            Error.Forbidden("Custom.Code", "Forbidden with extensions", new Dictionary<string, object?> { ["reason"] = "ban" })
        ];

        int[] expectedStatuses = [400, 401, 403, 404, 409, 413, 422, 503, 400, 400, 403];

        for (int i = 0; i < errors.Length; i++)
        {
            DefaultHttpContext httpContext = new();
            httpContext.Request.Path = "/api/v1/test";
            httpContext.Items[CorrelationIdMiddleware.ItemKey] = $"corr-{i}";
            httpContext.Response.Body = new MemoryStream();

            ProblemDetails pd = errors[i].ToProblemDetails(httpContext);
            pd.Status.Should().Be(expectedStatuses[i]);
            pd.Type.Should().NotBeNullOrWhiteSpace();
            pd.Title.Should().NotBeNullOrWhiteSpace();
            pd.Detail.Should().Be(errors[i].Message);
            pd.Extensions["correlationId"].Should().Be($"corr-{i}");

            IResult httpResult = Result.Failure(errors[i]).ToHttpResult();
            await httpResult.ExecuteAsync(httpContext);
            httpContext.Response.StatusCode.Should().Be(expectedStatuses[i]);
            httpContext.Response.ContentType.Should().StartWith("application/problem+json");
        }

        // ToProblemDetails sin HttpContext y sobrecargas Result / Result<T>
        Error noContextError = Error.PayloadTooLarge("Media.TooLarge", "Archivo excede el límite");
        ProblemDetails noContextPd = noContextError.ToProblemDetails();
        noContextPd.Status.Should().Be(413);
        noContextPd.Title.Should().Be("Media.TooLarge");
        noContextPd.Extensions.ContainsKey("correlationId").Should().BeTrue();

        Result.Success().ToHttpResult().Should().NotBeNull();
        Result.Success("ok").ToHttpResult().Should().NotBeNull();
        Result.Failure<string>(noContextError).ToHttpResult().Should().NotBeNull();
        Result.Failure(noContextError).ToProblemDetails().Status.Should().Be(413);
        Result.Failure<string>(noContextError).ToProblemDetails().Status.Should().Be(413);
        Result.Failure<string>(noContextError).ToCreatedAtResult("/api/v1/items/1").Should().NotBeNull();
        Result.Success("ok").ToCreatedAtResult("/api/v1/items/1").Should().NotBeNull();

        Action actSuccessResult = () => Result.Success().ToProblemDetails();
        actSuccessResult.Should().Throw<InvalidOperationException>();

        Action actSuccessGeneric = () => Result.Success("ok").ToProblemDetails();
        actSuccessGeneric.Should().Throw<InvalidOperationException>();
    }

    private static async Task AssertProblemDetailsShape(
        HttpResponseMessage response,
        HttpStatusCode expectedStatus,
        string expectedCorrelationId)
    {
        response.StatusCode.Should().Be(expectedStatus);

        using var stream = await response.Content.ReadAsStreamAsync();
        using var doc = await JsonDocument.ParseAsync(stream);
        var root = doc.RootElement;

        root.TryGetProperty("type", out var typeElem).Should().BeTrue("debe contener la propiedad 'type'");
        typeElem.GetString().Should().NotBeNullOrWhiteSpace();

        root.TryGetProperty("title", out var titleElem).Should().BeTrue("debe contener la propiedad 'title'");
        titleElem.GetString().Should().NotBeNullOrWhiteSpace();

        root.TryGetProperty("status", out var statusElem).Should().BeTrue("debe contener la propiedad 'status'");
        statusElem.GetInt32().Should().Be((int)expectedStatus);

        root.TryGetProperty("detail", out var detailElem).Should().BeTrue("debe contener la propiedad 'detail'");
        detailElem.GetString().Should().NotBeNullOrWhiteSpace();

        root.TryGetProperty("instance", out var instanceElem).Should().BeTrue("debe contener la propiedad 'instance'");
        instanceElem.GetString().Should().NotBeNullOrWhiteSpace();

        var cidFound = false;
        if (root.TryGetProperty("correlationId", out var cidElem))
        {
            cidElem.GetString().Should().Be(expectedCorrelationId);
            cidFound = true;
        }
        else if (root.TryGetProperty("extensions", out var extElem) && extElem.TryGetProperty("correlationId", out var extCidElem))
        {
            extCidElem.GetString().Should().Be(expectedCorrelationId);
            cidFound = true;
        }
        cidFound.Should().BeTrue("debe contener correlationId coincidente con X-Correlation-Id");

        response.Headers.Contains("X-Correlation-Id").Should().BeTrue();
        response.Headers.GetValues("X-Correlation-Id").First().Should().Be(expectedCorrelationId);

        var rawJson = root.GetRawText();
        rawJson.Should().NotContain("at System.");
        rawJson.Should().NotContain("StackTrace");
        rawJson.Should().NotContain("Exception");
        rawJson.Should().NotContain("collectionName");
        rawJson.Should().NotContain("store_reviews");
        rawJson.Should().NotContain("usuarios");
        rawJson.Should().NotContain("comercios");
    }
}
