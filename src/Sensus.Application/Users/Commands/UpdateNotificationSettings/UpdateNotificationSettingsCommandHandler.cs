using MediatR;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Users.Commands.UpdateNotificationSettings;
using Sensus.Application.Users.DTOs;
using Sensus.Domain.Common;
using Sensus.Domain.Entities;
using Sensus.Domain.Repositories;

namespace Sensus.Application.Users.Commands.UpdateNotificationSettings;

public sealed class UpdateNotificationSettingsCommandHandler : IRequestHandler<UpdateNotificationSettingsCommand, Result<NotificationSettingsDto>>
{
    private readonly INotificationSettingsRepository _settingsRepository;
    private readonly ICurrentUserService _currentUser;

    public UpdateNotificationSettingsCommandHandler(
        INotificationSettingsRepository settingsRepository,
        ICurrentUserService currentUser)
    {
        _settingsRepository = settingsRepository;
        _currentUser = currentUser;
    }

    public async Task<Result<NotificationSettingsDto>> Handle(
        UpdateNotificationSettingsCommand request,
        CancellationToken cancellationToken)
    {
        string? userId = _currentUser.UserId;
        if (string.IsNullOrWhiteSpace(userId) || !Guid.TryParse(userId, out Guid parsedUserId))
        {
            return Result.Failure<NotificationSettingsDto>(
                Error.Unauthorized("Settings.Unauthenticated", "Usuario no autenticado."));
        }

        NotificationSettings? settings = await _settingsRepository.GetByUserIdAsync(parsedUserId, cancellationToken);

        // Si no existe, crear con valores por defecto y luego actualizar (FR-044-05)
        if (settings is null)
        {
            settings = NotificationSettings.CreateDefaults(parsedUserId);
            await _settingsRepository.AddAsync(settings, cancellationToken);
        }

        // Validar radiusKm antes de aplicar (FR-044-03)
        if (request.RadiusKm.HasValue && (request.RadiusKm.Value < 1 || request.RadiusKm.Value > 50))
        {
            return Result.Failure<NotificationSettingsDto>(
                Error.Validation("Settings.InvalidRadius", "El radio debe estar entre 1 y 50 km."));
        }

        settings.Update(request.PushEnabled, request.OfferAlerts, request.SocialAlerts, request.RadiusKm);
        await _settingsRepository.UpdateAsync(settings, cancellationToken);

        return Result.Success(new NotificationSettingsDto(
            UserId: settings.UserId,
            PushEnabled: settings.PushEnabled,
            OfferAlerts: settings.OfferAlerts,
            SocialAlerts: settings.SocialAlerts,
            RadiusKm: settings.RadiusKm,
            CreatedAtUtc: settings.CreatedAtUtc,
            UpdatedAtUtc: settings.UpdatedAtUtc));
    }
}
