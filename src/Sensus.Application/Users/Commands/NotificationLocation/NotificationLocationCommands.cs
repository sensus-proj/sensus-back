using FluentValidation;
using MediatR;
using Microsoft.Extensions.Options;
using Sensus.Application.Common.Interfaces;
using Sensus.Domain.Common;
using Sensus.Domain.Repositories;

namespace Sensus.Application.Users.Commands.NotificationLocation;

public sealed record ShareNotificationLocationCommand(double? Latitude, double? Longitude)
    : IRequest<Result>, ISensitiveRequest;

public sealed class ShareNotificationLocationCommandValidator : AbstractValidator<ShareNotificationLocationCommand>
{
    public ShareNotificationLocationCommandValidator()
    {
        RuleFor(x => x.Latitude).NotNull().InclusiveBetween(-90d, 90d)
            .WithMessage("La latitud está fuera del rango admitido.");
        RuleFor(x => x.Longitude).NotNull().InclusiveBetween(-180d, 180d)
            .WithMessage("La longitud está fuera del rango admitido.");
    }
}

public sealed class NotificationLocationSettings
{
    public int LifetimeHours { get; init; } = 24;
}

public sealed class ShareNotificationLocationCommandHandler(
    ICurrentUserService currentUser,
    IDateTimeProvider clock,
    IUserRepository users,
    INotificationLocationRepository locations,
    IOptions<NotificationLocationSettings> settings) : IRequestHandler<ShareNotificationLocationCommand, Result>
{
    public async Task<Result> Handle(ShareNotificationLocationCommand request, CancellationToken cancellationToken)
    {
        if (currentUser.IsGuest || !Guid.TryParse(currentUser.UserId, out Guid userId))
        {
            return Result.Failure(Error.Unauthorized("Location.Unauthorized", "Debes iniciar sesión para compartir ubicación."));
        }

        if (request.Latitude is not double latitude || request.Longitude is not double longitude ||
            !double.IsFinite(latitude) || !double.IsFinite(longitude) ||
            latitude is < -90 or > 90 || longitude is < -180 or > 180)
        {
            return Result.Failure(Error.Validation("Location.InvalidCoordinates", "Las coordenadas no son válidas."));
        }

        DateTime now = clock.UtcNow;
        await locations.SaveAsync(userId, latitude, longitude, now,
            now.AddHours(settings.Value.LifetimeHours), cancellationToken);
        if (await users.GetByIdAsync(userId, cancellationToken) is null)
        {
            await locations.DeleteAsync(userId, cancellationToken);
            return Result.Failure(Error.Unauthorized("Location.Unauthorized", "La cuenta ya no está disponible."));
        }

        return Result.Success();
    }
}

public sealed record DeleteNotificationLocationCommand : IRequest<Result>;

public sealed class DeleteNotificationLocationCommandHandler(
    ICurrentUserService currentUser,
    INotificationLocationRepository locations) : IRequestHandler<DeleteNotificationLocationCommand, Result>
{
    public async Task<Result> Handle(DeleteNotificationLocationCommand request, CancellationToken cancellationToken)
    {
        if (currentUser.IsGuest || !Guid.TryParse(currentUser.UserId, out Guid userId))
        {
            return Result.Failure(Error.Unauthorized("Location.Unauthorized", "Debes iniciar sesión para retirar la ubicación."));
        }

        await locations.DeleteAsync(userId, cancellationToken);
        return Result.Success();
    }
}
