using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Sensus.Infrastructure.Services;
using Sensus.TestKit.Fakes;
using Xunit;

namespace Sensus.IntegrationTests.Infrastructure;

/// <summary>
/// Spec 029, FR-029-10 (excepción EX-004): el servicio de correo solo puede contactarse por HTTP sin TLS si el host es
/// el exceptuado y la excepción no ha vencido; en cualquier otro caso el envío se rechaza sin salir a la red.
/// </summary>
public sealed class MailerTransportPolicyTests
{
    private const string AllowedHost = "mailersensus.somee.com";

    private static readonly DateTime Now = new(2026, 11, 20, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTimeOffset Expiry = new(2026, 12, 20, 0, 0, 0, TimeSpan.Zero);

    private static MailerSettings Settings(
        string baseUrl = "http://mailersensus.somee.com",
        string allowedHost = AllowedHost,
        DateTimeOffset? allowedUntil = null,
        bool withExpiry = true) => new()
        {
            BaseUrl = baseUrl,
            ApiKey = "test-key",
            TimeoutSeconds = 5,
            InsecureHttpAllowedHost = allowedHost,
            InsecureHttpAllowedUntil = withExpiry ? allowedUntil ?? Expiry : null,
        };

    [Fact]
    public void Https_IsAlwaysAllowed()
    {
        var settings = Settings(allowedHost: string.Empty, withExpiry: false);

        MailerTransportPolicy.IsAllowed(new Uri("https://mail.example.com/api"), settings, Now).Should().BeTrue();
    }

    [Fact]
    public void Http_ToExceptedHostBeforeExpiry_IsAllowed()
    {
        MailerTransportPolicy.IsAllowed(new Uri("http://MailerSensus.Somee.com/api"), Settings(), Now).Should().BeTrue();
    }

    [Fact]
    public void Http_ToOtherHost_IsRejected()
    {
        MailerTransportPolicy.IsAllowed(new Uri("http://evil.example.com/api"), Settings(), Now).Should().BeFalse();
    }

    [Fact]
    public void Http_ToSubdomainOfExceptedHost_IsRejected()
    {
        MailerTransportPolicy.IsAllowed(new Uri("http://x.mailersensus.somee.com/api"), Settings(), Now).Should().BeFalse();
    }

    [Fact]
    public void Http_AtOrAfterExpiry_IsRejected()
    {
        var uri = new Uri("http://mailersensus.somee.com/api");

        MailerTransportPolicy.IsAllowed(uri, Settings(), Expiry.UtcDateTime).Should().BeFalse();
        MailerTransportPolicy.IsAllowed(uri, Settings(), Expiry.UtcDateTime.AddDays(1)).Should().BeFalse();
    }

    [Fact]
    public void Http_WithoutExpiry_IsRejected()
    {
        MailerTransportPolicy.IsAllowed(new Uri("http://mailersensus.somee.com/api"), Settings(withExpiry: false), Now)
            .Should().BeFalse();
    }

    [Fact]
    public void Http_WithoutAllowedHost_IsRejected()
    {
        MailerTransportPolicy.IsAllowed(new Uri("http://mailersensus.somee.com/api"), Settings(allowedHost: " "), Now)
            .Should().BeFalse();
    }

    [Fact]
    public void OtherScheme_IsRejected()
    {
        MailerTransportPolicy.IsAllowed(new Uri("ftp://mailersensus.somee.com/api"), Settings(), Now).Should().BeFalse();
    }

    [Fact]
    public void Settings_BindExpiryAsUtc_RegardlessOfLocalTimeZone()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["MailerSettings:InsecureHttpAllowedHost"] = AllowedHost,
                ["MailerSettings:InsecureHttpAllowedUntil"] = "2026-12-20T00:00:00Z",
            })
            .Build();

        var settings = configuration.GetSection(MailerSettings.SectionName).Get<MailerSettings>()!;

        settings.InsecureHttpAllowedUntil!.Value.UtcDateTime.Should().Be(Expiry.UtcDateTime);
    }

    [Fact]
    public async Task Sender_WithHttpToExceptedHost_SendsRequest()
    {
        var handler = new RecordingHandler();
        var sender = CreateSender(handler, Settings());

        await sender.SendEmailAsync("user@example.com", "Asunto", "<p>Hola</p>", "Hola");

        handler.Requests.Should().ContainSingle();
        handler.Requests[0].Should().Be(new Uri("http://mailersensus.somee.com/api/mail/sensus/send"));
    }

    [Fact]
    public async Task Sender_WithExpiredException_RejectsWithoutNetworkCall()
    {
        var handler = new RecordingHandler();
        var sender = CreateSender(handler, Settings(allowedUntil: new DateTimeOffset(Now.AddMinutes(-1))));

        var act = () => sender.SendEmailAsync("user@example.com", "Asunto", "<p>Hola</p>", "Hola");

        await act.Should().ThrowAsync<InvalidOperationException>();
        handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task Sender_WithHttpToOtherHost_RejectsWithoutNetworkCall()
    {
        var handler = new RecordingHandler();
        var sender = CreateSender(handler, Settings(baseUrl: "http://evil.example.com"));

        var act = () => sender.SendEmailAsync("user@example.com", "Asunto", "<p>Hola</p>", "Hola");

        await act.Should().ThrowAsync<InvalidOperationException>();
        handler.Requests.Should().BeEmpty();
    }

    private static HttpMailerEmailSender CreateSender(RecordingHandler handler, MailerSettings settings) =>
        new(
            new HttpClient(handler),
            Options.Create(settings),
            new FakeDateTimeProvider(Now),
            NullLogger<HttpMailerEmailSender>.Instance);

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public List<Uri> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request.RequestUri!);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }
}
