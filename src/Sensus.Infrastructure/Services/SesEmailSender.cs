using Amazon;
using Amazon.Runtime;
using Amazon.SimpleEmail;
using Amazon.SimpleEmail.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sensus.Application.Common.Interfaces;

namespace Sensus.Infrastructure.Services;

/// <summary>
/// Envío de correo transaccional mediante Amazon SES sobre TLS 1.2+ (Spec 029, FR-029-04 y FR-029-12).
/// No registra destinatarios, cuerpo del mensaje ni tokens; solo el identificador de mensaje devuelto por SES.
/// </summary>
public sealed class SesEmailSender : IEmailSender
{
    private readonly SesEmailSettings _settings;
    private readonly IAmazonSimpleEmailService? _client;
    private readonly ILogger<SesEmailSender> _logger;

    public SesEmailSender(
        IOptions<SesEmailSettings> options,
        IAmazonSimpleEmailService? client,
        ILogger<SesEmailSender> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        _settings = options.Value;
        _client = client;
        _logger = logger;
    }

    public async Task SendEmailAsync(
        string toEmail,
        string subject,
        string htmlBody,
        string? textBody = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(toEmail))
        {
            throw new ArgumentException("El destinatario del correo es obligatorio.", nameof(toEmail));
        }

        if (_settings.Simulate)
        {
            _logger.LogInformation(
                "[SES SIMULADO] Correo transaccional solicitado. Asunto: {Subject}. Destinatario redactado por seguridad.",
                subject);
            return;
        }

        if (_client is null)
        {
            throw new InvalidOperationException("El cliente de Amazon SES no está configurado.");
        }

        var body = new Body { Html = new Content(htmlBody) };
        if (!string.IsNullOrWhiteSpace(textBody))
        {
            body.Text = new Content(textBody);
        }

        var request = new SendEmailRequest
        {
            Source = _settings.From,
            Destination = new Destination { ToAddresses = [toEmail] },
            Message = new Message
            {
                Subject = new Content(subject),
                Body = body
            }
        };

        if (!string.IsNullOrWhiteSpace(_settings.ConfigurationSetName))
        {
            request.ConfigurationSetName = _settings.ConfigurationSetName;
        }

        try
        {
            var response = await _client.SendEmailAsync(request, cancellationToken);
            _logger.LogInformation(
                "Correo transaccional enviado mediante Amazon SES. MessageId={MessageId}",
                response.MessageId);
        }
        catch (AmazonServiceException ex)
        {
            _logger.LogError(
                ex,
                "Falló el envío mediante Amazon SES. Código={ErrorCode}, Estado={StatusCode}",
                ex.ErrorCode,
                ex.StatusCode);
            throw;
        }
    }
}
