using System.IO;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Sensus.Api.Extensions;
using Sensus.Application.Media.Commands.ModerateImage;
using Sensus.Application.Media.Commands.UploadMedia;
using Sensus.Application.Media.DTOs;

namespace Sensus.Api.Endpoints;

public static class MediaEndpoints
{
    private static readonly string[] _allowedMediaTypes = ["image/jpeg", "image/png", "image/webp"];

    public static IEndpointRouteBuilder MapMediaEndpoints(this IEndpointRouteBuilder app)
    {
        MapMediaGroup(app.MapGroup("/api/v1/media"), isV1: true);
        MapMediaGroup(app.MapGroup("/api/media"), isV1: false);

        return app;
    }

    private static void MapMediaGroup(RouteGroupBuilder group, bool isV1)
    {
        group.WithTags("Moderación Multimedia (OpenAI Omni-Moderation)")
            .RequireAuthorization(SecurityExtensions.GuestOrRegisteredPolicy);

        // Spec 022: Subida de imagen con validación, almacenamiento privado y moderación
        var upload = group.MapPost("/upload", async (
            [FromForm] IFormFile? file,
            [FromForm] string? textContext,
            ISender sender,
            CancellationToken ct) =>
        {
            if (file is null || file.Length == 0)
            {
                return Results.Problem(new ProblemDetails
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "Archivo requerido",
                    Detail = "No se proporcionó ningún archivo de imagen."
                });
            }

            const long MaxFileSizeBytes = 4_500_000; // 4,5 MB (FR-022-03)
            if (file.Length > MaxFileSizeBytes)
            {
                return Results.Problem(new ProblemDetails
                {
                    Status = StatusCodes.Status413RequestEntityTooLarge,
                    Title = "Archivo demasiado grande",
                    Detail = $"El archivo excede el tamaño máximo de 4,5 MB ({file.Length / 1024 / 1024:F2} MB)."
                });
            }

            var contentType = file.ContentType;
            // Validar que el content type sea uno permitido
            if (!_allowedMediaTypes.Contains(contentType, StringComparer.OrdinalIgnoreCase))
            {
                return Results.Problem(new ProblemDetails
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "Tipo de contenido no permitido",
                    Detail = "Solo se permiten imágenes JPEG, PNG y WebP."
                });
            }

            using var ms = new MemoryStream();
            await file.CopyToAsync(ms, ct);
            var imageBytes = ms.ToArray();

            var command = new UploadMediaCommand(imageBytes, contentType, textContext);
            var result = await sender.Send(command, ct);

            if (result.IsFailure)
            {
                var statusCode = result.Error.Type switch
                {
                    Domain.Common.ErrorType.Validation => StatusCodes.Status400BadRequest,
                    Domain.Common.ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
                    Domain.Common.ErrorType.Forbidden => StatusCodes.Status403Forbidden,
                    _ => StatusCodes.Status400BadRequest
                };

                return Results.Problem(new ProblemDetails
                {
                    Status = statusCode,
                    Title = result.Error.Code,
                    Detail = result.Error.Message
                });
            }

            var dto = result.Value;
            var statusCodeSuccess = dto.Status == "Approved"
                ? StatusCodes.Status201Created
                : StatusCodes.Status202Accepted;

            return Results.Created($"/api/v1/media/{dto.MediaId}", dto);
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

        var moderate = group.MapPost("/moderate", async (
            [FromBody] ModerateImageCommand command,
            ISender sender,
            CancellationToken ct) =>
        {
            var result = await sender.Send(command, ct);
            return result.ToHttpResult();
        })
        .WithSummary("Valida imágenes mediante OpenAI Omni-Moderation detectando contenido inapropiado antes de su almacenamiento")
        .Produces<ModerateImageResponseDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized);

        if (isV1) moderate.WithName("ModerateImage");
    }
}
