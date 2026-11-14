using MediatR;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Users.DTOs;
using Sensus.Domain.Common;

namespace Sensus.Application.Users.Commands.UpdateNotificationSettings;

/// <summary>
/// Spec 044: Comando para actualizar las preferencias de notificación del usuario autenticado.
/// </summary>
public sealed record UpdateNotificationSettingsCommand(
    bool? PushEnabled,
    bool? OfferAlerts,
    bool? SocialAlerts,
    int? RadiusKm) : IRequest<Result<NotificationSettingsDto>>;
