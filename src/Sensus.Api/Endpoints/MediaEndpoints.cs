using MediatR;
using Microsoft.AspNetCore.Mvc;
using Sensus.Api.Extensions;
using Sensus.Application.Media.Commands.ModerateImage;
using Sensus.Application.Media.DTOs;

namespace Sensus.Api.Endpoints;

public static class MediaEndpoints
{
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
