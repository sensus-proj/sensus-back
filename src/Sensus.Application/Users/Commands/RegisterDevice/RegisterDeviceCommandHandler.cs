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
        string? userId = _currentUser.UserId;
        if (string.IsNullOrWhiteSpace(userId) || !Guid.TryParse(userId, out Guid parsedUserId))
        {
            return Result.Failure<DeviceTokenResponseDto>(
                Error.Unauthorized("Device.Unauthenticated", "Usuario no autenticado."));
        }

        string token = request.Token.Trim();
        string deviceId = request.DeviceId.Trim();

        // Validar el token con el proveedor antes de registrar (FR-027-03, FR-027-05)
        PushResult validation = await _pushDispatcher.ValidateTokenAsync(token, cancellationToken);
        if (validation == PushResult.Unavailable)
        {
            return Result.Failure<DeviceTokenResponseDto>(new Error(
                "Device.ProviderUnavailable", "No se pudo validar el dispositivo en este momento.", ErrorType.Unavailable));
        }

        if (validation == PushResult.InvalidToken)
        {
            // El token es inválido, se elimina si existía (FR-027-05)
            Device? existingByToken = await _deviceRepository.GetByTokenAsync(token, cancellationToken);
            if (existingByToken?.UserId == parsedUserId)
            {
                await _deviceRepository.DeactivateByTokenAsync(existingByToken.Id, token, cancellationToken);
            }

            return Result.Failure<DeviceTokenResponseDto>(
                Error.Validation("Device.InvalidToken", "El token de dispositivo no es válido."));
        }

        Device? existing = await _deviceRepository.GetByUserAndDeviceIdAsync(parsedUserId, deviceId, cancellationToken);
        Device? binding = await _deviceRepository.GetByTokenAsync(token, cancellationToken);

        if (binding is not null && binding.Id != existing?.Id &&
            !await _deviceRepository.TryRemoveTokenBindingAsync(binding.Id, binding.UserId, token, cancellationToken))
        {
            return RegistrationConflict();
        }

        Device device = existing ?? Device.Create(parsedUserId, deviceId, request.Platform, token, _dateTimeProvider.UtcNow);
        try
        {
            if (existing is null)
            {
                await _deviceRepository.AddAsync(device, cancellationToken);
            }
            else
            {
                device.UpdateToken(token);
                device.Activate();
                await _deviceRepository.UpdateAsync(device, cancellationToken);
            }
        }
        catch (DeviceTokenConflictException)
        {
            return RegistrationConflict();
        }

        return Result.Success(new DeviceTokenResponseDto(
            DeviceId: device.DeviceId,
            Platform: device.Platform,
            Token: device.Token,
            IsActive: device.IsActive,
            RegisteredAtUtc: device.RegisteredAtUtc,
            Message: existing is null
                ? "Token de dispositivo registrado correctamente."
                : "Token de dispositivo actualizado correctamente."));
    }

    private static Result<DeviceTokenResponseDto> RegistrationConflict() =>
        Result.Failure<DeviceTokenResponseDto>(
            Error.Conflict("Device.RegistrationConflict", "El registro cambió durante la operación. Vuelva a intentarlo."));
}
