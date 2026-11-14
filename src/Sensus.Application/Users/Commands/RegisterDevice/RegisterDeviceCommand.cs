using MediatR;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Users.DTOs;
using Sensus.Domain.Common;

namespace Sensus.Application.Users.Commands.RegisterDevice;

/// <summary>
/// Spec 027: Comando para registrar o actualizar el token de dispositivo de un usuario para notificaciones push.
/// </summary>
public sealed record RegisterDeviceCommand(
    string DeviceId,
    string Platform,
    string Token) : IRequest<Result<DeviceTokenResponseDto>>;
