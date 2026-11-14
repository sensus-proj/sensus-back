using MediatR;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Users.DTOs;
using Sensus.Domain.Common;

namespace Sensus.Application.Users.Queries.GetNotificationSettings;

/// <summary>
/// Spec 044: Consulta para obtener las preferencias de notificación del usuario autenticado.
/// </summary>
public sealed record GetNotificationSettingsQuery : IRequest<Result<NotificationSettingsDto>>;
