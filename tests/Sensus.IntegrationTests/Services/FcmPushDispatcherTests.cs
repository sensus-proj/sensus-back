using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Sensus.Application.Common.Interfaces;
using Sensus.Infrastructure.Services;
using Xunit;

namespace Sensus.IntegrationTests.Services;

public sealed class FcmPushDispatcherTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task DispatchAsync_UsesFcmV1AndOnlySendsForRealDelivery(bool validateOnly)
    {
        string? body = null;
        using var http = new HttpClient(new ReplyHandler(async (request, cancellationToken) =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("/v1/projects/sensus-test/messages:send", request.RequestUri!.AbsolutePath);
            Assert.Equal("Bearer", request.Headers.Authorization!.Scheme);
            body = await request.Content!.ReadAsStringAsync(cancellationToken);
            return Respond(HttpStatusCode.OK, """{"name":"projects/sensus-test/messages/1"}""");
        }))
        {
            BaseAddress = new Uri("https://fcm.googleapis.com/")
        };
        var dispatcher = CreateDispatcher(http);

        PushResult result = validateOnly
            ? await dispatcher.ValidateTokenAsync("test-device")
            : await dispatcher.SendAsync("test-device", "Test notification", "Test message");

        Assert.Equal(PushResult.Accepted, result);
        using var payload = JsonDocument.Parse(body!);
        Assert.Equal(validateOnly, payload.RootElement.GetProperty("validate_only").GetBoolean());
        JsonElement message = payload.RootElement.GetProperty("message");
        Assert.Equal("test-device", message.GetProperty("token").GetString());
        Assert.Equal(!validateOnly, message.TryGetProperty("notification", out _));
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound, """{"error":{"details":[{"@type":"type.googleapis.com/google.firebase.fcm.v1.FcmError","errorCode":"UNREGISTERED"}]}}""", PushResult.InvalidToken)]
    [InlineData(HttpStatusCode.NotFound, """{"error":{"status":"NOT_FOUND"}}""", PushResult.Unavailable)]
    [InlineData(HttpStatusCode.Unauthorized, """{"error":{"status":"UNAUTHENTICATED"}}""", PushResult.Unavailable)]
    [InlineData(HttpStatusCode.OK, "not-json", PushResult.Unavailable)]
    [InlineData(HttpStatusCode.OK, "{}", PushResult.Unavailable)]
    public async Task SendAsync_ClassifiesResponse(HttpStatusCode status, string body, PushResult expected)
    {
        using var http = new HttpClient(new ReplyHandler((_, _) => Task.FromResult(Respond(status, body))))
        {
            BaseAddress = new Uri("https://fcm.googleapis.com/")
        };

        Assert.Equal(expected, await CreateDispatcher(http).SendAsync("test-device", "Title", "Body"));
    }

    [Fact]
    public async Task SendAsync_PreservesTokenOnTimeout()
    {
        using var http = new HttpClient(new ReplyHandler((_, _) => throw new TaskCanceledException()))
        {
            BaseAddress = new Uri("https://fcm.googleapis.com/")
        };

        Assert.Equal(PushResult.Unavailable, await CreateDispatcher(http).SendAsync("test-device", "Title", "Body"));
    }

    private static FcmPushDispatcher CreateDispatcher(HttpClient http) =>
        new(http, _ => Task.FromResult(("sensus-test", Guid.NewGuid().ToString())),
            NullLogger<FcmPushDispatcher>.Instance);

    private static HttpResponseMessage Respond(HttpStatusCode status, string body) =>
        new(status) { Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json") };

    private sealed class ReplyHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> reply) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            reply(request, cancellationToken);
    }
}
