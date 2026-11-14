using MediatR;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Users.Commands.RegisterDevice;
using Sensus.Application.Users.DTOs;
using Sensus.Domain.Common;
using Sensus.Domain.Entities;
using Sensus.Domain.Repositories;

namespace Sensus.Application.Users.Commands.RegisterDevice;

public sealed class RegisterDeviceCommandHandler : IRequestHandler<RegisterDeviceCommand, Result<DeviceTokenResponseDto>>
{
    private readonly IDeviceRepository _deviceRepository;
    private readonly IPushDispatcher _pushDispatcher;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider _dateTimeProvider;

    public RegisterDeviceCommandHandler(
        IDeviceRepository deviceRepository,
        IPushDispatcher pushDispatcher,
        ICurrentUserService currentUser,
        IDateTimeProvider dateTimeProvider)
    {
        _deviceRepository = deviceRepository;
        _pushDispatcher = pushDispatcher;
        _currentUser = currentUser;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<Result<DeviceTokenResponseDto>> Handle(
        RegisterDeviceCommand request,
        CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId;
        if (string.IsNullOrWhiteSpace(userId) || !Guid.TryParse(userId, out var parsedUserId))
        {
            return Result.Failure<DeviceTokenResponseDto>(
                Error.Unauthorized("Device.Unauthenticated", "Usuario no autenticado."));
        }

        // Validar el token con el proveedor antes de registrar (FR-027-03, FR-027-05)
        var tokenValid = await _pushDispatcher.ValidateTokenAsync(request.Token, cancellationToken);
        if (!tokenValid)
        {
            // El token es inválido, se elimina si existía (FR-027-05)
            var existingByToken = await _deviceRepository.GetByTokenAsync(request.Token, cancellationToken);
            if (existingByToken is not null)
            {
                existingByToken.Deactivate();
                await _deviceRepository.UpdateAsync(existingByToken, cancellationToken);
            }

            return Result.Failure<DeviceTokenResponseDto>(
                Error.Validation("Device.InvalidToken", "El token de dispositivo no es válido."));
        }

        // Buscar si ya existe un dispositivo con este deviceId para este usuario (FR-027-02)
        var existing = await _deviceRepository.GetByUserAndDeviceIdAsync(parsedUserId, request.DeviceId, cancellationToken);

        if (existing is not null)
        {
            // Actualizar token existente sin duplicar (FR-027-02)
            existing.UpdateToken(request.Token);
            existing.MarkUsed();
            existing.Activate();
            await _deviceRepository.UpdateAsync(existing, cancellationToken);

            return Result.Success(new DeviceTokenResponseDto(
                DeviceId: existing.DeviceId,
                Platform: existing.Platform,
                Token: existing.Token,
                IsActive: existing.IsActive,
                RegisteredAtUtc: existing.RegisteredAtUtc,
                Message: "Token de dispositivo actualizado correctamente."));
        }

        // Verificar si el token ya pertenece a otro usuario y reasignar (FR-027-06)
        var deviceByToken = await _deviceRepository.GetByTokenAsync(request.Token, cancellationToken);
        if (deviceByToken is not null && deviceByToken.UserId != parsedUserId)
        {
            // Reasignar: desactivar para el usuario anterior
            deviceByToken.Deactivate();
            await _deviceRepository.UpdateAsync(deviceByToken, cancellationToken);
        }

        // Crear nuevo dispositivo (FR-027-01)
        var device = Device.Create(
            parsedUserId,
            request.DeviceId,
            request.Platform,
            request.Token,
            _dateTimeProvider.UtcNow);

        await _deviceRepository.AddAsync(device, cancellationToken);

        return Result.Success(new DeviceTokenResponseDto(
            DeviceId: device.DeviceId,
            Platform: device.Platform,
            Token: device.Token,
            IsActive: device.IsActive,
            RegisteredAtUtc: device.RegisteredAtUtc,
            Message: "Token de dispositivo registrado correctamente."));
    }
}
