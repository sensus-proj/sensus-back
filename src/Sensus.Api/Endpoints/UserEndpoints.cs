using MediatR;
using Microsoft.AspNetCore.Mvc;
using Sensus.Api.Extensions;
using Sensus.Application.Common.Interfaces;
using Sensus.Application.Users.Commands.DeleteAccount;
using Sensus.Application.Users.Commands.GeneratePhotoSignature;
using Sensus.Application.Users.Commands.RegisterDevice;
using Sensus.Application.Users.Commands.UpdateNotificationSettings;
using Sensus.Application.Users.Commands.UpdateProfile;
using Sensus.Application.Users.DTOs;
using Sensus.Application.Users.Queries.GetNotificationSettings;
using Sensus.Application.Users.Queries.GetProfile;
using Sensus.Domain.Common;

namespace Sensus.Api.Endpoints;

public static class UserEndpoints
{
    public static IEndpointRouteBuilder MapUserEndpoints(this IEndpointRouteBuilder app)
    {
        MapUserGroup(app.MapGroup("/api/v1/users"), isV1: true);

        return app;
    }

    private static void MapUserGroup(RouteGroupBuilder group, bool isV1)
    {
        group.WithTags("Usuarios y Perfil (SE-006 / SE-009)");

        // SE-006: Obtener información del perfil
        RouteHandlerBuilder getProfile = group.MapGet("/profile", async (
            ISender sender,
            CancellationToken ct) =>
        {
            Result<UserDto> result = await sender.Send(new GetProfileQuery(), ct);
            return result.ToHttpResult();
        })
        .RequireAuthorization(SecurityExtensions.RegisteredUserPolicy)
        .WithSummary("SE-006: Consulta el perfil del usuario autenticado")
        .Produces<UserDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound);

        if (isV1) getProfile.WithName("GetProfile");

        // SE-006: Actualizar información personal
        RouteHandlerBuilder updateProfile = group.MapPut("/profile", async (
            [FromBody] UpdateProfileCommand command,
            ISender sender,
            CancellationToken ct) =>
        {
            Result<UserDto> result = await sender.Send(command, ct);
            return result.ToHttpResult();
        })
        .RequireAuthorization(SecurityExtensions.RegisteredUserPolicy)
        .WithSummary("SE-006: Actualiza el nombre completo y/o la foto de perfil en Cloudinary")
        .Produces<UserDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound);

        if (isV1) updateProfile.WithName("UpdateProfile");

        // SE-006: Solicitar firma de subida para Cloudinary
        RouteHandlerBuilder photoSignature = group.MapPost("/photo-signature", async (
            ISender sender,
            CancellationToken ct) =>
        {
            Result<CloudinarySignatureResponse> result = await sender.Send(new GeneratePhotoSignatureCommand(), ct);
            return result.ToHttpResult();
        })
        .RequireAuthorization(SecurityExtensions.RegisteredUserPolicy)
        .WithSummary("SE-006: Genera parámetros firmados para subida directa de fotos a Cloudinary")
        .Produces<CloudinarySignatureResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden);

        if (isV1) photoSignature.WithName("GeneratePhotoSignature");

        // Spec 027: Registro de token de dispositivo para notificaciones push
        RouteHandlerBuilder registerDevice = group.MapPut("/device-token", async (
            [FromBody] RegisterDeviceCommand command,
            ISender sender,
            CancellationToken ct) =>
        {
            Result<DeviceTokenResponseDto> result = await sender.Send(command, ct);
            return result.ToHttpResult();
        })
        .RequireAuthorization(SecurityExtensions.RegisteredUserPolicy)
        .WithSummary("Spec 027: Registra o actualiza el token de dispositivo del usuario para notificaciones push")
        .Produces<DeviceTokenResponseDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden);

        if (isV1) registerDevice.WithName("RegisterDevice");

        // Spec 044: Obtener preferencias de notificación del usuario
        RouteHandlerBuilder getSettings = group.MapGet("/settings", async (
            ISender sender,
            CancellationToken ct) =>
        {
            Result<NotificationSettingsDto> result = await sender.Send(new GetNotificationSettingsQuery(), ct);
            return result.ToHttpResult();
        })
        .RequireAuthorization(SecurityExtensions.RegisteredUserPolicy)
        .WithSummary("Spec 044: Obtiene las preferencias de notificación del usuario autenticado")
        .Produces<NotificationSettingsDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound);

        if (isV1) getSettings.WithName("GetNotificationSettings");

        // Spec 044: Actualizar preferencias de notificación del usuario
        RouteHandlerBuilder updateSettings = group.MapPut("/settings", async (
            [FromBody] UpdateNotificationSettingsCommand command,
            ISender sender,
            CancellationToken ct) =>
        {
            Result<NotificationSettingsDto> result = await sender.Send(command, ct);
            return result.ToHttpResult();
        })
        .RequireAuthorization(SecurityExtensions.RegisteredUserPolicy)
        .WithSummary("Spec 044: Actualiza las preferencias de notificación del usuario autenticado")
        .Produces<NotificationSettingsDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound);

        if (isV1) updateSettings.WithName("UpdateNotificationSettings");

        // SE-009: Eliminación definitiva de cuenta y datos asociados
        RouteHandlerBuilder deleteAccount = group.MapDelete("/account", async (
            [FromBody] DeleteAccountCommand command,
            ISender sender,
            CancellationToken ct) =>
        {
            Result result = await sender.Send(command, ct);
            return result.ToHttpResult();
        })
        .RequireAuthorization(SecurityExtensions.RegisteredUserPolicy)
        .WithSummary("SE-009: Elimina la cuenta y todos sus datos personales y comerciales de forma definitiva previa confirmación de contraseña")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound);

        if (isV1) deleteAccount.WithName("DeleteAccount");
    }
}
