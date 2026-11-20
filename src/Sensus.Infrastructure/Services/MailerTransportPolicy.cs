namespace Sensus.Infrastructure.Services;

/// <summary>
/// Decide si el servicio de correo puede contactarse por el esquema de la URL configurada (FR-029-10).
/// HTTPS siempre se permite. HTTP sin TLS solo hacia el host exceptuado por EX-004 y antes de su fecha de
/// expiración; cualquier otro caso se rechaza (fail-closed).
/// </summary>
public static class MailerTransportPolicy
{
    public static bool IsAllowed(Uri requestUri, MailerSettings settings, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(requestUri);
        ArgumentNullException.ThrowIfNull(settings);

        if (!requestUri.IsAbsoluteUri)
        {
            return false;
        }

        if (requestUri.Scheme == Uri.UriSchemeHttps)
        {
            return true;
        }

        if (requestUri.Scheme != Uri.UriSchemeHttp)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(settings.InsecureHttpAllowedHost) || settings.InsecureHttpAllowedUntil is null)
        {
            return false;
        }

        var allowedUntilUtc = settings.InsecureHttpAllowedUntil.Value.UtcDateTime;

        return string.Equals(requestUri.IdnHost, settings.InsecureHttpAllowedHost.Trim(), StringComparison.OrdinalIgnoreCase)
            && nowUtc < allowedUntilUtc;
    }
}
