using System.Globalization;
using Microsoft.Extensions.Logging;
using Sensus.Application.Common.Interfaces;
using Sensus.Domain.Entities;
using Sensus.Domain.Repositories;

namespace Sensus.Application.Sanctions.Services;

/// <summary>
/// Notificación push de sanciones (Spec 020, FR-020-08). Es una comunicación de seguridad transaccional: no
/// depende de las preferencias de alertas del usuario. El mensaje no incluye evidencia ni categorías, y un
/// fallo de entrega se registra sin interrumpir el bloqueo ni la reversión.
/// </summary>
public sealed class SanctionNotifier : ISanctionNotifier
{
    private readonly IDeviceRepository _devices;
    private readonly IPushDispatcher _push;
    private readonly ILogger<SanctionNotifier> _logger;

    public SanctionNotifier(IDeviceRepository devices, IPushDispatcher push, ILogger<SanctionNotifier> logger)
    {
        _devices = devices;
        _push = push;
        _logger = logger;
    }

    public Task NotifyAppliedAsync(Sanction sanction, CancellationToken cancellationToken = default)
    {
        string until = sanction.EndsAtUtc is { } end
            ? $"hasta el {end.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture)} UTC"
            : "de forma permanente";

        return SendAsync(
            sanction,
            "Tu cuenta fue suspendida",
            $"Tu acceso a Sensus está suspendido {until} por incumplir las normas de la comunidad.",
            "sanction_applied",
            cancellationToken);
    }

    public Task NotifyRevertedAsync(Sanction sanction, CancellationToken cancellationToken = default) =>
        SendAsync(
            sanction,
            "Tu acceso fue restablecido",
            "Revisamos tu caso y ya puedes volver a iniciar sesión en Sensus.",
            "sanction_reverted",
            cancellationToken);

    private async Task SendAsync(
        Sanction sanction,
        string title,
        string body,
        string kind,
        CancellationToken cancellationToken)
    {
        try
        {
            List<Device> devices = await _devices.GetActiveByUserAsync(sanction.UserId, cancellationToken);
            var data = new Dictionary<string, string> { ["type"] = kind };
            foreach (Device device in devices)
            {
                await _push.SendAsync(device.Token, title, body, data, cancellationToken);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "No se pudo entregar la notificación de sanción {SanctionId}.", sanction.Id);
        }
    }
}
