using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Sensus.Application.Common.Behaviors;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Feed.Commands.CreateFeedPost;
using Sensus.Application.Feed.DTOs;
using Sensus.Domain.Common;

namespace Sensus.Application.UnitTests.Common.Behaviors;

public class LoggingBehaviorTests
{
    private readonly ILogger<LoggingBehavior<SampleSensitiveRequest, string>> _sensitiveLogger =
        Substitute.For<ILogger<LoggingBehavior<SampleSensitiveRequest, string>>>();

    private readonly ILogger<LoggingBehavior<SampleNormalRequest, string>> _normalLogger =
        Substitute.For<ILogger<LoggingBehavior<SampleNormalRequest, string>>>();

    private readonly ILogger<LoggingBehavior<CreateFeedPostCommand, Result<FeedPostDto>>> _feedPostLogger =
        Substitute.For<ILogger<LoggingBehavior<CreateFeedPostCommand, Result<FeedPostDto>>>>();

    private readonly ILogger<PerformanceBehavior<CreateFeedPostCommand, Result<FeedPostDto>>> _perfLogger =
        Substitute.For<ILogger<PerformanceBehavior<CreateFeedPostCommand, Result<FeedPostDto>>>>();

    public sealed record SampleSensitiveRequest(string Password) : IRequest<string>, ISensitiveRequest;
    public sealed record SampleNormalRequest(string Query) : IRequest<string>;

    [Fact]
    public async Task Handle_ShouldRedactPayload_WhenCreateFeedPostCommandIsLogged()
    {
        // Ref: FR-014-05 - comandos con contenido libre no filtran su carga útil en logs ni propiedades estructuradas
        var behavior = new LoggingBehavior<CreateFeedPostCommand, Result<FeedPostDto>>(_feedPostLogger);
        var command = new CreateFeedPostCommand("contenido secreto del feed");
        var expected = Result.Failure<FeedPostDto>(Error.Validation("Test.Ok", "ok"));
        RequestHandlerDelegate<Result<FeedPostDto>> next = _ => Task.FromResult(expected);

        var response = await behavior.Handle(command, next, CancellationToken.None);

        response.Should().Be(expected);
        _feedPostLogger.Received().Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => ContainsRedactedNoticeAndNoSecret(o, "contenido secreto")),
            null,
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task PerformanceBehavior_ShouldRedactSensitiveRequest_WhenSlow()
    {
        var behavior = new PerformanceBehavior<CreateFeedPostCommand, Result<FeedPostDto>>(_perfLogger);
        var command = new CreateFeedPostCommand("contenido secreto lento");
        var expected = Result.Failure<FeedPostDto>(Error.Validation("Test.Ok", "ok"));
        RequestHandlerDelegate<Result<FeedPostDto>> next = async ct =>
        {
            await Task.Delay(550, ct);
            return expected;
        };

        var response = await behavior.Handle(command, next, CancellationToken.None);

        response.Should().Be(expected);
        _perfLogger.Received().Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Is<object>(o =>
                o.ToString()!.Contains("[REDACTED]")
                && NoStructuredPropertyContains(o, "contenido secreto")),
            null,
            Arg.Any<Func<object, Exception?, string>>());
    }

    private static bool ContainsRedactedNoticeAndNoSecret(object state, string secret) =>
        state.ToString()!.Contains("[DATOS REDACTADOS POR SEGURIDAD]")
        && NoStructuredPropertyContains(state, secret);

    private static bool NoStructuredPropertyContains(object state, string secret)
    {
        if (state.ToString()!.Contains(secret, StringComparison.Ordinal))
        {
            return false;
        }

        if (state is IEnumerable<KeyValuePair<string, object?>> props)
        {
            return props.All(kv => kv.Value?.ToString()?.Contains(secret, StringComparison.Ordinal) != true);
        }

        return true;
    }

    [Fact]
    public async Task Handle_ShouldRedactPayload_WhenRequestIsSensitive()
    {
        var behavior = new LoggingBehavior<SampleSensitiveRequest, string>(_sensitiveLogger);
        var request = new SampleSensitiveRequest("P@ssw0rd123!");
        RequestHandlerDelegate<string> next = (ct) => Task.FromResult("OK");

        var response = await behavior.Handle(request, next, CancellationToken.None);

        response.Should().Be("OK");
        _sensitiveLogger.Received().Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("[DATOS REDACTADOS POR SEGURIDAD]") && !o.ToString()!.Contains("P@ssw0rd123!")),
            null,
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task Handle_ShouldLogPayload_WhenRequestIsNotSensitive()
    {
        var behavior = new LoggingBehavior<SampleNormalRequest, string>(_normalLogger);
        var request = new SampleNormalRequest("test-query");
        RequestHandlerDelegate<string> next = (ct) => Task.FromResult("OK");

        var response = await behavior.Handle(request, next, CancellationToken.None);

        response.Should().Be("OK");
        _normalLogger.Received().Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("SampleNormalRequest")),
            null,
            Arg.Any<Func<object, Exception?, string>>());
    }
}
