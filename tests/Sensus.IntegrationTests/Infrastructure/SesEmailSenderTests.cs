using Amazon;
using Amazon.Runtime;
using Amazon.SimpleEmail;
using Amazon.SimpleEmail.Model;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Sensus.Infrastructure.Services;
using Xunit;

namespace Sensus.IntegrationTests.Infrastructure;

/// <summary>
/// Spec 029, FR-029-04 y FR-029-12: el transporte de correo es Amazon SES sobre TLS y nunca registra
/// destinatarios, cuerpo del mensaje ni tokens.
/// </summary>
public sealed class SesEmailSenderTests
{
    private const string From = "noreply@sensus.com";
    private const string To = "user@example.com";
    private const string Html = "<p>Restablece tu contraseña</p>";
    private const string Text = "Restablece tu contraseña";

    private readonly IAmazonSimpleEmailService _client = Substitute.For<IAmazonSimpleEmailService>();
    private readonly ILogger<SesEmailSender> _logger = Substitute.For<ILogger<SesEmailSender>>();

    [Fact]
    public async Task Simulate_ShouldNotCallSes_AndShouldRedactRecipientInLogs()
    {
        var sender = Create(simulate: true);

        await sender.SendEmailAsync(To, "Asunto", Html, Text);

        await _client.DidNotReceive().SendEmailAsync(Arg.Any<SendEmailRequest>(), Arg.Any<CancellationToken>());
        _logger.Received().Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("Asunto") && !o.ToString()!.Contains(To)),
            null,
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task SendEmailAsync_ShouldUseVerifiedSenderAndBothBodies()
    {
        _client.SendEmailAsync(Arg.Any<SendEmailRequest>(), Arg.Any<CancellationToken>())
            .Returns(new SendEmailResponse { MessageId = "msg-029" });
        var sender = Create(simulate: false);

        await sender.SendEmailAsync(To, "Asunto", Html, Text);

        await _client.Received(1).SendEmailAsync(
            Arg.Is<SendEmailRequest>(request =>
                request.Source == From &&
                request.Destination.ToAddresses.Count == 1 &&
                request.Destination.ToAddresses[0] == To &&
                request.Message.Subject.Data == "Asunto" &&
                request.Message.Body.Html.Data == Html &&
                request.Message.Body.Text.Data == Text),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendEmailAsync_ShouldConfigureSetAndRegion_WhenProvided()
    {
        _client.SendEmailAsync(Arg.Any<SendEmailRequest>(), Arg.Any<CancellationToken>())
            .Returns(new SendEmailResponse { MessageId = "msg-030" });
        var sender = Create(simulate: false, configurationSet: "sensus-transactional", region: "us-east-1");

        await sender.SendEmailAsync(To, "Asunto", Html);

        await _client.Received(1).SendEmailAsync(
            Arg.Is<SendEmailRequest>(request => request.ConfigurationSetName == "sensus-transactional"),
            Arg.Any<CancellationToken>());
        RegionEndpoint.GetBySystemName("us-east-1").Should().NotBeNull();
    }

    [Fact]
    public async Task SendEmailAsync_WhenSesFails_ShouldRethrowWithoutLoggingPii()
    {
        _client.SendEmailAsync(Arg.Any<SendEmailRequest>(), Arg.Any<CancellationToken>())
            .Returns<Task<SendEmailResponse>>(_ => throw new AmazonServiceException("fallo SES"));
        var sender = Create(simulate: false);

        var act = () => sender.SendEmailAsync(To, "Asunto", Html, Text);

        await act.Should().ThrowAsync<AmazonServiceException>();
        _logger.Received().Log(
            LogLevel.Error,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => !o.ToString()!.Contains(To) && !o.ToString()!.Contains(Html)),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    private SesEmailSender Create(
        bool simulate,
        string configurationSet = "",
        string region = "") =>
        new(
            Options.Create(new SesEmailSettings
            {
                From = From,
                Region = region,
                ConfigurationSetName = configurationSet,
                Simulate = simulate
            }),
            simulate ? null : _client,
            _logger);
}
