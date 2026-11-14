using MediatR;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Users.DTOs;
using Sensus.Domain.Common;
using Sensus.Domain.Entities;
using Sensus.Domain.Repositories;

namespace Sensus.Application.Users.Queries.GetNotificationSettings;

public sealed class GetNotificationSettingsQueryHandler : IRequestHandler<GetNotificationSettingsQuery, Result<NotificationSettingsDto>>
{
    private readonly INotificationSettingsRepository _settingsRepository;
    private readonly ICurrentUserService _currentUser;

    public GetNotificationSettingsQueryHandler(
        INotificationSettingsRepository settingsRepository,
        ICurrentUserService currentUser)
    {
        _settingsRepository = settingsRepository;
        _currentUser = currentUser;
    }

    public async Task<Result<NotificationSettingsDto>> Handle(
        GetNotificationSettingsQuery request,
        CancellationToken cancellationToken)
    {
        string? userId = _currentUser.UserId;
        if (string.IsNullOrWhiteSpace(userId) || !Guid.TryParse(userId, out Guid parsedUserId))
        {
            return Result.Failure<NotificationSettingsDto>(
                Error.Unauthorized("Settings.Unauthenticated", "Usuario no autenticado."));
        }

        NotificationSettings? settings = await _settingsRepository.GetByUserIdAsync(parsedUserId, cancellationToken);

        // Si no existe, devolver valores por defecto (FR-044-05)
        if (settings is null)
        {
            NotificationSettings defaults = NotificationSettings.CreateDefaults(parsedUserId);
            await _settingsRepository.AddAsync(defaults, cancellationToken);

            return Result.Success(new NotificationSettingsDto(
                UserId: defaults.UserId,
                PushEnabled: defaults.PushEnabled,
                OfferAlerts: defaults.OfferAlerts,
                SocialAlerts: defaults.SocialAlerts,
                RadiusKm: defaults.RadiusKm,
                CreatedAtUtc: defaults.CreatedAtUtc));
        }

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
