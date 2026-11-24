using System.IO;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Sensus.Api.Extensions;
using Sensus.Application.Media.Commands.ReleaseQuarantinedMedia;
using Sensus.Application.Media.Commands.UploadMedia;
using Sensus.Application.Media.DTOs;
using Sensus.Domain.Common;

namespace Sensus.Api.Endpoints;

public static class MediaEndpoints
{
    private static readonly string[] _allowedMediaTypes = ["image/jpeg", "image/png", "image/webp"];

    public static IEndpointRouteBuilder MapMediaEndpoints(this IEndpointRouteBuilder app)
    {
        MapMediaGroup(app.MapGroup("/api/v1/media"), isV1: true);

        // Spec 019 / FR-019-04: liberación manual de contenido en cuarentena por un administrador.
        app.MapPost("/api/v1/media/{id:guid}/release", async (
            Guid id,
            ISender sender,
            CancellationToken ct) =>
        {
            Result<ReleasedMediaDto> result = await sender.Send(new ReleaseQuarantinedMediaCommand(id), ct);
            return result.ToHttpResult();
        })
        .RequireAuthorization(SecurityExtensions.AdminOnlyPolicy)
        .RequireRateLimiting(SecurityExtensions.SensitiveWritePolicy)
        .WithTags("Moderación Multimedia (OpenAI Omni-Moderation)")
        .WithName("ReleaseQuarantinedMedia")
        .WithSummary("Spec 019: Libera un activo en cuarentena tras su revisión manual por un administrador")
        .Produces<ReleasedMediaDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict)
        .ProducesProblem(StatusCodes.Status429TooManyRequests);

        return app;
    }

    private static void MapMediaGroup(RouteGroupBuilder group, bool isV1)
    {
        group.WithTags("Moderación Multimedia (OpenAI Omni-Moderation)")
            .RequireAuthorization(SecurityExtensions.GuestOrRegisteredPolicy);

        // Spec 022: Subida de imagen con validación, almacenamiento privado y moderación
        RouteHandlerBuilder upload = group.MapPost("/upload", async (
            IFormFile? file,
            [FromForm] string? textContext,
            ISender sender,
            CancellationToken ct) =>
        {
            if (file is null || file.Length == 0)
            {
                return Result.Failure(Error.Validation(
                    "Archivo requerido",
                    "No se proporcionó ningún archivo de imagen.")).ToHttpResult();
            }

            const long MaxFileSizeBytes = 4_500_000; // 4,5 MB (FR-022-03)
            if (file.Length > MaxFileSizeBytes)
            {
                return Result.Failure(Error.PayloadTooLarge(
                    "Archivo demasiado grande",
                    $"El archivo excede el tamaño máximo de 4,5 MB ({file.Length / 1024 / 1024:F2} MB).")).ToHttpResult();
            }

            string contentType = file.ContentType;
            // Validar que el content type sea uno permitido
            if (!_allowedMediaTypes.Contains(contentType, StringComparer.OrdinalIgnoreCase))
            {
                return Result.Failure(Error.Validation(
                    "Tipo de contenido no permitido",
                    "Solo se permiten imágenes JPEG, PNG y WebP.")).ToHttpResult();
            }

            using MemoryStream ms = new();
            await file.CopyToAsync(ms, ct);
            byte[] imageBytes = ms.ToArray();

            UploadMediaCommand command = new UploadMediaCommand(imageBytes, contentType, textContext);
            Result<UploadMediaResponseDto> result = await sender.Send(command, ct);

            if (result.IsFailure)
            {
                return result.ToHttpResult();
            }

            UploadMediaResponseDto dto = result.Value;
            int statusCodeSuccess = dto.Status == "Approved"
                ? StatusCodes.Status201Created
                : StatusCodes.Status202Accepted;

            return Results.Json(dto, statusCode: statusCodeSuccess);
        })
        .DisableAntiforgery()
        .WithSummary("Spec 022: Sube una imagen, valida tipo y tamaño, almacena en Cloudinary y modera antes de exponerla")
        .Produces<UploadMediaResponseDto>(StatusCodes.Status201Created)
        .Produces<UploadMediaResponseDto>(StatusCodes.Status202Accepted)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status413RequestEntityTooLarge)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status429TooManyRequests);

        if (isV1) upload.WithName("UploadMedia");
    }
}
